using Google.OrTools.Sat;
using Timetable.Application.Models;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Solver;

/// <summary>Signalée lorsque le modèle est manifestement infaisable avant même de lancer la recherche.</summary>
internal sealed class ModelInfeasibleException : Exception
{
    public ModelInfeasibleException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Construit le modèle CP-SAT d'un emploi du temps (§8.2, §8.3) puis en extrait la solution.
/// </summary>
internal sealed class ModelContext
{
    private readonly SolverRequest _request;
    private readonly CpModel _model;

    private readonly Dictionary<int, TimeSlot> _slotsById;
    private readonly Dictionary<int, Subject> _subjectsById;
    private readonly Dictionary<int, Teacher> _teachersById;
    private readonly Dictionary<(int DayId, int SlotIndex), TimeSlot> _slotIndex;
    private readonly Dictionary<int, List<TimeSlot>> _slotsByDay;

    private readonly Dictionary<(int C, int S, int T, int L), BoolVar> _singles = new();
    private readonly Dictionary<(int C, int S, int T, int L), BoolVar> _blocks = new();
    private readonly Dictionary<BoolVar, List<SolverLesson>> _lessons = new();

    private readonly Dictionary<(int C, int L), List<IntVar>> _classSlotVars = new();
    private readonly Dictionary<(int T, int L), List<IntVar>> _teacherSlotVars = new();
    private readonly Dictionary<(int C, int S, int L), List<IntVar>> _classSubjectSlotVars = new();
    private readonly Dictionary<(int C, int S, int D), List<IntVar>> _blockVarsByClassSubjectDay = new();

    private readonly Dictionary<(int SubjectId, int StartSlotId), List<int>> _blockCoverCache = new();
    private readonly List<(IntVar Var, int Weight)> _penalties = new();

    public ModelContext(SolverRequest request, CpModel model)
    {
        _request = request;
        _model = model;
        _slotsById = request.Slots.ToDictionary(s => s.Id);
        _subjectsById = request.Subjects.ToDictionary(s => s.Id);
        _teachersById = request.Teachers.ToDictionary(t => t.Id);
        _slotIndex = SlotLayout.BuildIndex(request.Slots);
        _slotsByDay = request.Slots
            .GroupBy(s => s.DayId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SlotIndex).ToList());
    }

    private IReadOnlySet<(int TeacherId, int SlotId)> Unavailable => _request.Unavailabilities;

    public void CreateVariables()
    {
        var requirements = _request.Requirements.Where(r => r.WeeklySessions > 0).ToList();
        foreach (var requirement in requirements)
        {
            if (!_subjectsById.TryGetValue(requirement.SubjectId, out var subject))
            {
                continue;
            }

            var teacherIds = ResolveTeachers(requirement, subject);
            if (teacherIds.Count == 0)
            {
                throw new ModelInfeasibleException(
                    $"La matière {subject.Name} doit être enseignée mais aucun enseignant de cette matière n'est disponible.");
            }

            if (subject.RequiresBlocks)
            {
                CreateBlockVariables(requirement, subject, teacherIds);
            }
            else
            {
                CreateSingleVariables(requirement, subject, teacherIds);
            }
        }

        CreateLockedVariables();
    }

    private List<int> ResolveTeachers(ClassSubjectRequirement requirement, Subject subject)
    {
        if (requirement.FixedTeacherId is { } fixedId)
        {
            return _teachersById.TryGetValue(fixedId, out var teacher) && teacher.SubjectId == subject.Id
                ? new List<int> { fixedId }
                : new List<int>();
        }

        return _request.Teachers
            .Where(t => t.SubjectId == subject.Id)
            .Select(t => t.Id)
            .ToList();
    }

    private void CreateSingleVariables(ClassSubjectRequirement requirement, Subject subject, List<int> teacherIds)
    {
        foreach (var teacherId in teacherIds)
        {
            foreach (var slot in _request.Slots)
            {
                if (Unavailable.Contains((teacherId, slot.Id)))
                {
                    continue;
                }

                var variable = _model.NewBoolVar($"x_c{requirement.ClassId}_s{subject.Id}_t{teacherId}_l{slot.Id}");
                _singles[(requirement.ClassId, subject.Id, teacherId, slot.Id)] = variable;
                Register(variable, requirement.ClassId, subject.Id, teacherId, slot.Id);
                AddLesson(variable, new SolverLesson(requirement.ClassId, subject.Id, teacherId, slot.Id, null));
            }
        }
    }

    private void CreateBlockVariables(ClassSubjectRequirement requirement, Subject subject, List<int> teacherIds)
    {
        var blockSize = subject.BlockSize;
        var starts = _request.Slots
            .Where(slot => SlotLayout.CanStartBlock(slot, blockSize, _slotIndex))
            .ToList();

        if (starts.Count == 0)
        {
            throw new ModelInfeasibleException(
                $"Impossible de placer un bloc de {blockSize} séances de {subject.Name} : " +
                "aucun couple de créneaux consécutifs sans pause n'est disponible.");
        }

        foreach (var teacherId in teacherIds)
        {
            foreach (var start in starts)
            {
                var cover = BlockCover(subject.Id, start, blockSize);
                if (cover.Any(slot => Unavailable.Contains((teacherId, slot.Id))))
                {
                    continue;
                }

                var variable = _model.NewBoolVar(
                    $"b_c{requirement.ClassId}_s{subject.Id}_t{teacherId}_l{start.Id}");
                _blocks[(requirement.ClassId, subject.Id, teacherId, start.Id)] = variable;
                var blockKey = $"blk-{requirement.ClassId}-{subject.Id}-{teacherId}-{start.Id}";

                foreach (var slot in cover)
                {
                    Register(variable, requirement.ClassId, subject.Id, teacherId, slot.Id);
                }

                AddLesson(
                    variable,
                    cover.Select(slot => new SolverLesson(
                        requirement.ClassId, subject.Id, teacherId, slot.Id, blockKey)).ToList());

                AddTo(_blockVarsByClassSubjectDay, (requirement.ClassId, subject.Id, start.DayId), variable);
            }
        }
    }

    private void CreateLockedVariables()
    {
        if (_request.LockedLessons.Count == 0)
        {
            return;
        }

        // Séances isolées verrouillées.
        foreach (var lesson in _request.LockedLessons.Where(l =>
                     !_subjectsById.TryGetValue(l.SubjectId, out var s) || !s.RequiresBlocks))
        {
            var key = (lesson.ClassId, lesson.SubjectId, lesson.TeacherId, lesson.TimeSlotId);
            if (_singles.TryGetValue(key, out _))
            {
                continue;
            }

            if (!_slotsById.ContainsKey(lesson.TimeSlotId))
            {
                continue;
            }

            var variable = _model.NewBoolVar($"lock_x_c{lesson.ClassId}_s{lesson.SubjectId}_t{lesson.TeacherId}_l{lesson.TimeSlotId}");
            _singles[key] = variable;
            Register(variable, lesson.ClassId, lesson.SubjectId, lesson.TeacherId, lesson.TimeSlotId);
            AddLesson(variable, new SolverLesson(lesson.ClassId, lesson.SubjectId, lesson.TeacherId, lesson.TimeSlotId, null));
        }

        // Blocs verrouillés : regroupés par (classe, matière, bloc).
        var blockGroups = _request.LockedLessons
            .Where(l => _subjectsById.TryGetValue(l.SubjectId, out var s) && s.RequiresBlocks)
            .GroupBy(l => (l.ClassId, l.SubjectId, l.BlockKey));

        foreach (var group in blockGroups)
        {
            var subject = _subjectsById[group.Key.SubjectId];
            var lessons = group.OrderBy(l => _slotsById[l.TimeSlotId].SlotIndex).ToList();
            if (lessons.Count != subject.BlockSize)
            {
                continue;
            }

            var start = lessons[0];
            var teacherId = lessons[0].TeacherId;
            var key = (start.ClassId, start.SubjectId, teacherId, start.TimeSlotId);
            if (_blocks.ContainsKey(key))
            {
                continue;
            }

            var cover = BlockCover(start.SubjectId, _slotsById[start.TimeSlotId], subject.BlockSize);
            if (cover.Count != subject.BlockSize)
            {
                continue;
            }

            var variable = _model.NewBoolVar($"lock_b_c{start.ClassId}_s{start.SubjectId}_t{teacherId}_l{start.TimeSlotId}");
            _blocks[key] = variable;
            var blockKey = $"blk-{start.ClassId}-{start.SubjectId}-{teacherId}-{start.TimeSlotId}";
            foreach (var slot in cover)
            {
                Register(variable, start.ClassId, start.SubjectId, teacherId, slot.Id);
            }

            AddLesson(
                variable,
                cover.Select(slot => new SolverLesson(start.ClassId, start.SubjectId, teacherId, slot.Id, blockKey)).ToList());
            AddTo(_blockVarsByClassSubjectDay, (start.ClassId, start.SubjectId, _slotsById[start.TimeSlotId].DayId), variable);
        }
    }

    public void AddHardConstraints()
    {
        AddRequirementCounts();
        AddUniquenessConstraints();
        AddLockedConstraints();
        AddOneClassPerTeacherOption();
    }

    private void AddRequirementCounts()
    {
        foreach (var requirement in _request.Requirements.Where(r => r.WeeklySessions > 0))
        {
            var subject = _subjectsById[requirement.SubjectId];
            if (subject.RequiresBlocks)
            {
                if (requirement.WeeklySessions % subject.BlockSize != 0)
                {
                    throw new ModelInfeasibleException(
                        $"Le volume de {subject.Name} ({requirement.WeeklySessions}) n'est pas un multiple " +
                        $"de la taille de bloc ({subject.BlockSize}).");
                }

                var blocks = _blocks
                    .Where(kv => kv.Key.C == requirement.ClassId && kv.Key.S == requirement.SubjectId)
                    .Select(kv => (IntVar)kv.Value)
                    .ToList();

                _model.Add(Sum(blocks) == requirement.WeeklySessions / subject.BlockSize);
            }
            else
            {
                var singles = _singles
                    .Where(kv => kv.Key.C == requirement.ClassId && kv.Key.S == requirement.SubjectId)
                    .Select(kv => (IntVar)kv.Value)
                    .ToList();

                _model.Add(Sum(singles) == requirement.WeeklySessions);
            }
        }
    }

    private void AddUniquenessConstraints()
    {
        foreach (var (key, variables) in _classSlotVars)
        {
            _model.Add(Sum(variables) <= 1);
        }

        foreach (var (_, variables) in _teacherSlotVars)
        {
            _model.Add(Sum(variables) <= 1);
        }
    }

    private void AddLockedConstraints()
    {
        foreach (var lesson in _request.LockedLessons)
        {
            if (_subjectsById.TryGetValue(lesson.SubjectId, out var subject) && subject.RequiresBlocks)
            {
                continue; // traité via les variables de bloc créées plus haut
            }

            if (_singles.TryGetValue((lesson.ClassId, lesson.SubjectId, lesson.TeacherId, lesson.TimeSlotId), out var variable))
            {
                _model.Add(variable == 1);
            }
        }

        var blockGroups = _request.LockedLessons
            .Where(l => _subjectsById.TryGetValue(l.SubjectId, out var s) && s.RequiresBlocks)
            .GroupBy(l => (l.ClassId, l.SubjectId, l.BlockKey));

        foreach (var group in blockGroups)
        {
            var lessons = group.OrderBy(l => _slotsById[l.TimeSlotId].SlotIndex).ToList();
            var start = lessons[0];
            if (_blocks.TryGetValue((start.ClassId, start.SubjectId, start.TeacherId, start.TimeSlotId), out var variable))
            {
                _model.Add(variable == 1);
            }
        }
    }

    private void AddOneClassPerTeacherOption()
    {
        if (!_request.Options.OneClassPerTeacherWeek)
        {
            return;
        }

        var classOfTeacher = new Dictionary<(int T, int C), BoolVar>();

        foreach (var (key, variable) in _singles)
        {
            var u = GetOrCreateClassUsage(classOfTeacher, key.T, key.C);
            _model.Add(variable <= u);
        }

        foreach (var (key, variable) in _blocks)
        {
            var u = GetOrCreateClassUsage(classOfTeacher, key.T, key.C);
            _model.Add(variable <= u);
        }

        foreach (var group in classOfTeacher.GroupBy(kv => kv.Key.T))
        {
            _model.Add(Sum(group.Select(kv => (IntVar)kv.Value).ToList()) <= 1);
        }
    }

    private BoolVar GetOrCreateClassUsage(Dictionary<(int T, int C), BoolVar> map, int teacherId, int classId)
    {
        if (!map.TryGetValue((teacherId, classId), out var variable))
        {
            variable = _model.NewBoolVar($"u_t{teacherId}_c{classId}");
            map[(teacherId, classId)] = variable;
        }

        return variable;
    }

    public void AddObjective()
    {
        var weights = _request.Weights;

        if (weights[SoftConstraintCode.Holes] > 0)
        {
            AddHolesPenalty(weights[SoftConstraintCode.Holes]);
        }

        if (weights[SoftConstraintCode.MaxTwoSameSubjectPerDay] > 0 ||
            weights[SoftConstraintCode.SpreadSubjectOverDays] > 0)
        {
            AddSubjectDayPenalties(weights);
        }

        if (weights[SoftConstraintCode.OneBlockPerDay] > 0)
        {
            AddBlockPerDayPenalty(weights[SoftConstraintCode.OneBlockPerDay]);
        }

        if (weights[SoftConstraintCode.TeacherBalance] > 0)
        {
            AddTeacherBalancePenalty(weights[SoftConstraintCode.TeacherBalance]);
        }

        if (_request.Options.AvoidSportAroundBreak && weights[SoftConstraintCode.NoSportAroundBreak] > 0)
        {
            AddSportAroundBreakPenalty(weights[SoftConstraintCode.NoSportAroundBreak]);
        }

        if (_penalties.Count == 0)
        {
            return;
        }

        var grouped = _penalties
            .GroupBy(p => p.Var)
            .Select(g => (Var: g.Key, Weight: g.Sum(x => x.Weight)))
            .Where(g => g.Weight != 0)
            .ToList();

        var expressions = grouped.Select(g => (LinearExpr)g.Var).ToArray();
        var coefficients = grouped.Select(g => (long)g.Weight).ToArray();
        _model.Minimize(LinearExpr.WeightedSum(expressions, coefficients));
    }

    private void AddHolesPenalty(int weight)
    {
        var classActive = BuildClassActive();
        foreach (var schoolClass in _request.Classes)
        {
            foreach (var (dayId, _) in _slotsByDay)
            {
                var active = _slotsByDay[dayId]
                    .Select(slot => classActive[(schoolClass.Id, slot.Id)])
                    .ToList();
                AddPenalties(HolesPenalty(active, $"h_c{schoolClass.Id}_d{dayId}"), weight);
            }
        }
    }

    private void AddSubjectDayPenalties(GenerationWeights weights)
    {
        var weightMax = weights[SoftConstraintCode.MaxTwoSameSubjectPerDay];
        var weightSpread = weights[SoftConstraintCode.SpreadSubjectOverDays];

        foreach (var requirement in _request.Requirements.Where(r => r.WeeklySessions > 0))
        {
            foreach (var (dayId, slots) in _slotsByDay)
            {
                var terms = new List<IntVar>();
                foreach (var slot in slots)
                {
                    if (_classSubjectSlotVars.TryGetValue((requirement.ClassId, requirement.SubjectId, slot.Id), out var list))
                    {
                        terms.AddRange(list);
                    }
                }

                if (terms.Count == 0)
                {
                    continue;
                }

                var count = _model.NewIntVar(0, requirement.WeeklySessions, $"cnt_c{requirement.ClassId}_s{requirement.SubjectId}_d{dayId}");
                _model.Add(count == Sum(terms));

                if (weightMax > 0)
                {
                    var excess = _model.NewIntVar(0, requirement.WeeklySessions, $"exc2_{count.Name}");
                    _model.Add(excess >= count - 2);
                    _penalties.Add((excess, weightMax));
                }

                if (weightSpread > 0)
                {
                    var excess = _model.NewIntVar(0, requirement.WeeklySessions, $"exc1_{count.Name}");
                    _model.Add(excess >= count - 1);
                    _penalties.Add((excess, weightSpread));
                }
            }
        }
    }

    private void AddBlockPerDayPenalty(int weight)
    {
        foreach (var (key, variables) in _blockVarsByClassSubjectDay)
        {
            if (variables.Count <= 1)
            {
                continue;
            }

            var count = _model.NewIntVar(0, variables.Count, $"blk_c{key.C}_s{key.S}_d{key.D}");
            _model.Add(count == Sum(variables));
            var excess = _model.NewIntVar(0, variables.Count, $"blk_exc_{count.Name}");
            _model.Add(excess >= count - 1);
            _penalties.Add((excess, weight));
        }
    }

    private void AddTeacherBalancePenalty(int weight)
    {
        var teacherActive = BuildTeacherActive();
        var dayIds = _slotsByDay.Keys.OrderBy(d => d).ToList();
        var dayCount = Math.Max(1, dayIds.Count);

        foreach (var teacher in _request.Teachers)
        {
            var dayLoads = new List<IntVar>();
            foreach (var dayId in dayIds)
            {
                var active = _slotsByDay[dayId]
                    .Select(slot => teacherActive[(teacher.Id, slot.Id)])
                    .ToList();

                var load = _model.NewIntVar(0, _slotsByDay[dayId].Count, $"tl_t{teacher.Id}_d{dayId}");
                _model.Add(load == Sum(active));
                dayLoads.Add(load);

                AddPenalties(HolesPenalty(active, $"th_t{teacher.Id}_d{dayId}"), weight);
            }

            var total = _model.NewIntVar(0, _request.Slots.Count, $"tt_t{teacher.Id}");
            _model.Add(total == Sum(dayLoads));

            // Équilibre : écart |jourCount × charge_jour − charge_totale|.
            foreach (var load in dayLoads)
            {
                var deviation = _model.NewIntVar(0, _request.Slots.Count * dayCount, $"dev_{load.Name}");
                _model.AddAbsEquality(
                    deviation,
                    LinearExpr.WeightedSum(
                        new LinearExpr[] { load, total },
                        new long[] { dayCount, -1 }));
                _penalties.Add((deviation, weight));
            }
        }
    }

    private void AddSportAroundBreakPenalty(int weight)
    {
        foreach (var subject in _request.Subjects.Where(s => s.RequiresBlocks))
        {
            foreach (var start in _request.Slots.Where(s => SlotLayout.CanStartBlock(s, subject.BlockSize, _slotIndex)))
            {
                if (!TouchesBreak(start, subject.BlockSize))
                {
                    continue;
                }

                foreach (var (key, variable) in _blocks.Where(kv => kv.Key.S == subject.Id && kv.Key.L == start.Id))
                {
                    _penalties.Add((variable, weight));
                }
            }
        }
    }

    private bool TouchesBreak(TimeSlot start, int blockSize)
    {
        if (start.FollowsBreak)
        {
            return true;
        }

        var after = _slotIndex.TryGetValue((start.DayId, start.SlotIndex + blockSize), out var slot) && slot.FollowsBreak;
        return after;
    }

    private Dictionary<(int C, int L), BoolVar> BuildClassActive()
    {
        var result = new Dictionary<(int C, int L), BoolVar>();
        foreach (var schoolClass in _request.Classes)
        {
            foreach (var slot in _request.Slots)
            {
                var variable = _model.NewBoolVar($"ca_c{schoolClass.Id}_l{slot.Id}");
                if (_classSlotVars.TryGetValue((schoolClass.Id, slot.Id), out var list))
                {
                    _model.Add(variable == Sum(list));
                }
                else
                {
                    _model.Add(variable == 0);
                }

                result[(schoolClass.Id, slot.Id)] = variable;
            }
        }

        return result;
    }

    private Dictionary<(int T, int L), BoolVar> BuildTeacherActive()
    {
        var result = new Dictionary<(int T, int L), BoolVar>();
        foreach (var teacher in _request.Teachers)
        {
            foreach (var slot in _request.Slots)
            {
                var variable = _model.NewBoolVar($"ta_t{teacher.Id}_l{slot.Id}");
                if (_teacherSlotVars.TryGetValue((teacher.Id, slot.Id), out var list))
                {
                    _model.Add(variable == Sum(list));
                }
                else
                {
                    _model.Add(variable == 0);
                }

                result[(teacher.Id, slot.Id)] = variable;
            }
        }

        return result;
    }

    private IReadOnlyList<IntVar> HolesPenalty(IReadOnlyList<BoolVar> active, string name)
    {
        var gaps = new List<IntVar>();
        var n = active.Count;
        if (n < 3)
        {
            return gaps;
        }

        var prefix = new IntVar[n];
        var suffix = new IntVar[n];

        for (var i = 0; i < n; i++)
        {
            prefix[i] = _model.NewBoolVar($"{name}_p{i}");
            if (i == 0)
            {
                _model.Add(prefix[i] == active[i]);
            }
            else
            {
                _model.Add(prefix[i] >= prefix[i - 1]);
                _model.Add(prefix[i] >= active[i]);
                _model.Add(prefix[i] <= prefix[i - 1] + active[i]);
            }
        }

        for (var i = n - 1; i >= 0; i--)
        {
            suffix[i] = _model.NewBoolVar($"{name}_s{i}");
            if (i == n - 1)
            {
                _model.Add(suffix[i] == active[i]);
            }
            else
            {
                _model.Add(suffix[i] >= suffix[i + 1]);
                _model.Add(suffix[i] >= active[i]);
                _model.Add(suffix[i] <= suffix[i + 1] + active[i]);
            }
        }

        for (var i = 1; i < n - 1; i++)
        {
            var gap = _model.NewBoolVar($"{name}_g{i}");
            _model.Add(gap >= prefix[i - 1] + suffix[i + 1] + (1 - active[i]) - 2);
            _model.Add(gap <= prefix[i - 1]);
            _model.Add(gap <= suffix[i + 1]);
            _model.Add(gap <= 1 - active[i]);
            gaps.Add(gap);
        }

        return gaps;
    }

    private void AddPenalties(IEnumerable<IntVar> variables, int weight)
    {
        if (weight == 0)
        {
            return;
        }

        foreach (var variable in variables)
        {
            _penalties.Add((variable, weight));
        }
    }

    public IReadOnlyList<SolverLesson> Extract(CpSolver solver)
    {
        var lessons = new List<SolverLesson>();
        foreach (var (variable, mapped) in _lessons)
        {
            if (solver.BooleanValue(variable))
            {
                lessons.AddRange(mapped);
            }
        }

        return lessons;
    }

    private void Register(BoolVar variable, int classId, int subjectId, int teacherId, int slotId)
    {
        AddTo(_classSlotVars, (classId, slotId), variable);
        AddTo(_teacherSlotVars, (teacherId, slotId), variable);
        AddTo(_classSubjectSlotVars, (classId, subjectId, slotId), variable);
    }

    private void AddLesson(BoolVar variable, SolverLesson lesson) => _lessons[variable] = new List<SolverLesson> { lesson };

    private void AddLesson(BoolVar variable, List<SolverLesson> lessons) => _lessons[variable] = lessons;

    private static void AddTo<TKey>(Dictionary<TKey, List<IntVar>> map, TKey key, IntVar variable)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<IntVar>();
            map[key] = list;
        }

        list.Add(variable);
    }

    private List<TimeSlot> BlockCover(int subjectId, TimeSlot start, int blockSize)
    {
        if (_blockCoverCache.TryGetValue((subjectId, start.Id), out var ids))
        {
            return ids.Select(id => _slotsById[id]).ToList();
        }

        var cover = SlotLayout.BlockSlots(start, blockSize, _slotIndex).ToList();
        _blockCoverCache[(subjectId, start.Id)] = cover.Select(s => s.Id).ToList();
        return cover;
    }

    private static LinearExpr Sum(IReadOnlyList<IntVar> variables)
        => LinearExpr.Sum(variables.Select(v => (LinearExpr)v).ToArray());
}
