using Timetable.Domain.Common;
using Timetable.Domain.Entities;

namespace Timetable.Domain.Scheduling;

/// <summary>
/// Valide un emploi du temps contre les contraintes dures HC-01 à HC-09 (§6.1).
/// Utilisé par le panneau « Conflits » (FR-12) et par les tests d'acceptation (AC-02, AC-03, AC-04…).
/// </summary>
public sealed class ScheduleValidator
{
    private readonly ScheduleContext _context;

    public ScheduleValidator(ScheduleContext context) => _context = context;

    private sealed record Session(
        int? LessonId,
        int ClassId,
        int SubjectId,
        int TeacherId,
        int SlotId,
        string? BlockKey)
    {
        public static Session FromLesson(Lesson lesson)
            => new(lesson.Id, lesson.ClassId, lesson.SubjectId, lesson.TeacherId, lesson.TimeSlotId, lesson.BlockKey);

        public static Session FromPlacement(PlacementRequest request)
            => new(null, request.ClassId, request.SubjectId, request.TeacherId, request.SlotId, request.BlockKey);
    }

    /// <summary>Valide l'ensemble de l'emploi du temps (contraintes dures + cohérence des volumes, HC-03).</summary>
    public IReadOnlyList<ScheduleConflict> ValidateAll(
        IEnumerable<Lesson> lessons,
        bool checkRequirementCounts = true)
    {
        var list = lessons.ToList();
        var sessions = list.Select(Session.FromLesson).ToList();
        var conflicts = CheckHard(sessions);

        if (checkRequirementCounts)
        {
            CheckRequirementCounts(conflicts, sessions);
        }

        return conflicts;
    }

    /// <summary>
    /// Valide l'insertion de nouvelles séances (déplacement, échange, glisser-déposer) sur l'emploi du
    /// temps courant. Les séances listées dans <paramref name="ignoreLessonIds"/> sont retirées avant contrôle.
    /// </summary>
    public IReadOnlyList<ScheduleConflict> ValidatePlacements(
        IEnumerable<Lesson> currentLessons,
        IReadOnlyList<PlacementRequest> placements,
        IEnumerable<int>? ignoreLessonIds = null,
        bool checkRequirementCounts = false)
    {
        var ignored = ignoreLessonIds is null
            ? new HashSet<int>()
            : new HashSet<int>(ignoreLessonIds);

        var sessions = currentLessons
            .Where(l => !ignored.Contains(l.Id))
            .Select(Session.FromLesson)
            .Concat(placements.Select(Session.FromPlacement))
            .ToList();

        var conflicts = CheckHard(sessions);
        if (checkRequirementCounts)
        {
            CheckRequirementCounts(conflicts, sessions);
        }

        return conflicts;
    }

    private List<ScheduleConflict> CheckHard(IReadOnlyList<Session> sessions)
    {
        var conflicts = new List<ScheduleConflict>();

        CheckClassSlotUniqueness(conflicts, sessions);
        CheckTeacherSlotUniqueness(conflicts, sessions);
        CheckTeacherSubjectMatch(conflicts, sessions);
        CheckUnavailabilities(conflicts, sessions);
        CheckOneClassPerTeacherWeek(conflicts, sessions);
        CheckBlocks(conflicts, sessions);
        CheckFixedTeachers(conflicts, sessions);

        return conflicts;
    }

    private void CheckClassSlotUniqueness(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        foreach (var group in sessions.GroupBy(s => (s.ClassId, s.SlotId)))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            var first = group.First();
            conflicts.Add(new ScheduleConflict(
                "HC-01",
                $"La classe {_context.ClassName(first.ClassId)} a {group.Count()} séances sur le créneau {_context.SlotLabel(first.SlotId)}.",
                ConflictSeverity.Error,
                ClassId: first.ClassId,
                SlotId: first.SlotId));
        }
    }

    private void CheckTeacherSlotUniqueness(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        foreach (var group in sessions.GroupBy(s => (s.TeacherId, s.SlotId)))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            var first = group.First();
            var classes = group
                .Select(s => _context.ClassName(s.ClassId))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            conflicts.Add(new ScheduleConflict(
                "HC-02",
                $"L'enseignant {_context.TeacherName(first.TeacherId)} est déjà en cours avec la classe " +
                $"{classes.First()} à ce créneau ({_context.SlotLabel(first.SlotId)}).",
                ConflictSeverity.Error,
                TeacherId: first.TeacherId,
                SlotId: first.SlotId));
        }
    }

    private void CheckTeacherSubjectMatch(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        foreach (var session in sessions)
        {
            if (!_context.TeachersById.TryGetValue(session.TeacherId, out var teacher))
            {
                continue;
            }

            if (teacher.SubjectId != session.SubjectId)
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-05",
                    $"L'enseignant {teacher.DisplayName} ({_context.SubjectName(teacher.SubjectId)}) ne peut pas " +
                    $"assurer la matière {_context.SubjectName(session.SubjectId)} pour la classe " +
                    $"{_context.ClassName(session.ClassId)}.",
                    ConflictSeverity.Error,
                    ClassId: session.ClassId,
                    TeacherId: session.TeacherId,
                    SubjectId: session.SubjectId,
                    SlotId: session.SlotId));
            }
        }
    }

    private void CheckUnavailabilities(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        if (_context.Unavailabilities.Count == 0)
        {
            return;
        }

        foreach (var session in sessions)
        {
            if (_context.Unavailabilities.Contains((session.TeacherId, session.SlotId)))
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-07",
                    $"L'enseignant {_context.TeacherName(session.TeacherId)} est indisponible le " +
                    $"{_context.SlotLabel(session.SlotId)} (classe {_context.ClassName(session.ClassId)}).",
                    ConflictSeverity.Error,
                    ClassId: session.ClassId,
                    TeacherId: session.TeacherId,
                    SlotId: session.SlotId));
            }
        }
    }

    private void CheckOneClassPerTeacherWeek(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        if (!_context.OneClassPerTeacherWeek)
        {
            return;
        }

        foreach (var group in sessions.GroupBy(s => s.TeacherId))
        {
            var classes = group.Select(s => s.ClassId).Distinct().ToList();
            if (classes.Count > 1)
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-09",
                    $"L'option « un enseignant = une seule classe » est active, mais " +
                    $"{_context.TeacherName(group.Key)} intervient dans {classes.Count} classes.",
                    ConflictSeverity.Error,
                    TeacherId: group.Key));
            }
        }
    }

    private void CheckBlocks(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        foreach (var session in sessions)
        {
            if (!_context.SubjectsById.TryGetValue(session.SubjectId, out var subject) || !subject.RequiresBlocks)
            {
                continue;
            }

            if (string.IsNullOrEmpty(session.BlockKey))
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-06",
                    $"La matière {subject.Name} exige des blocs de {subject.BlockSize} séances : la séance isolée de la " +
                    $"classe {_context.ClassName(session.ClassId)} le {_context.SlotLabel(session.SlotId)} est interdite.",
                    ConflictSeverity.Error,
                    ClassId: session.ClassId,
                    SubjectId: session.SubjectId,
                    SlotId: session.SlotId));
            }
        }

        var blockGroups = sessions
            .Where(s => _context.SubjectsById.TryGetValue(s.SubjectId, out var sub) && sub.RequiresBlocks)
            .Where(s => !string.IsNullOrEmpty(s.BlockKey))
            .GroupBy(s => (s.ClassId, s.SubjectId, BlockKey: s.BlockKey!));

        foreach (var group in blockGroups)
        {
            var subject = _context.SubjectsById[group.Key.SubjectId];
            var items = group.ToList();

            if (items.Count != subject.BlockSize)
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-06",
                    $"Bloc incomplet de {subject.Name} pour la classe {_context.ClassName(group.Key.ClassId)} : " +
                    $"{items.Count} séance(s) au lieu de {subject.BlockSize}.",
                    ConflictSeverity.Error,
                    ClassId: group.Key.ClassId,
                    SubjectId: group.Key.SubjectId));
                continue;
            }

            var teachers = items.Select(i => i.TeacherId).Distinct().ToList();
            if (teachers.Count > 1)
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-06",
                    $"Le bloc de {subject.Name} de la classe {_context.ClassName(group.Key.ClassId)} fait intervenir " +
                    "plusieurs enseignants.",
                    ConflictSeverity.Error,
                    ClassId: group.Key.ClassId,
                    SubjectId: group.Key.SubjectId));
                continue;
            }

            var slots = items
                .Select(i => _context.SlotsById.TryGetValue(i.SlotId, out var slot) ? slot : null)
                .Where(s => s is not null)
                .Select(s => s!)
                .OrderBy(s => s.SlotIndex)
                .ToList();

            if (slots.Count != subject.BlockSize)
            {
                continue;
            }

            var sameDay = slots.All(s => s.DayId == slots[0].DayId);
            var contiguous = true;
            for (var i = 1; i < slots.Count; i++)
            {
                if (slots[i].SlotIndex != slots[i - 1].SlotIndex + 1 || slots[i].FollowsBreak)
                {
                    contiguous = false;
                    break;
                }
            }

            if (!sameDay || !contiguous)
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-06",
                    $"Le bloc de {subject.Name} de la classe {_context.ClassName(group.Key.ClassId)} n'occupe pas " +
                    $"{subject.BlockSize} créneaux consécutifs du même jour sans pause intercalée.",
                    ConflictSeverity.Error,
                    ClassId: group.Key.ClassId,
                    SubjectId: group.Key.SubjectId));
            }
        }
    }

    private void CheckFixedTeachers(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        foreach (var requirement in _context.Requirements)
        {
            if (requirement.FixedTeacherId is not { } fixedTeacherId)
            {
                continue;
            }

            foreach (var session in sessions.Where(s =>
                         s.ClassId == requirement.ClassId && s.SubjectId == requirement.SubjectId &&
                         s.TeacherId != fixedTeacherId))
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-08",
                    $"La classe {_context.ClassName(requirement.ClassId)} doit suivre " +
                    $"{_context.SubjectName(requirement.SubjectId)} avec {_context.TeacherName(fixedTeacherId)}, " +
                    $"mais {_context.TeacherName(session.TeacherId)} est affecté.",
                    ConflictSeverity.Error,
                    ClassId: requirement.ClassId,
                    TeacherId: session.TeacherId,
                    SubjectId: requirement.SubjectId,
                    SlotId: session.SlotId));
            }
        }
    }

    private void CheckRequirementCounts(List<ScheduleConflict> conflicts, IReadOnlyList<Session> sessions)
    {
        foreach (var requirement in _context.Requirements)
        {
            var actual = sessions.Count(s =>
                s.ClassId == requirement.ClassId && s.SubjectId == requirement.SubjectId);

            if (actual != requirement.WeeklySessions)
            {
                conflicts.Add(new ScheduleConflict(
                    "HC-03",
                    $"La classe {_context.ClassName(requirement.ClassId)} doit avoir {requirement.WeeklySessions} " +
                    $"séance(s) de {_context.SubjectName(requirement.SubjectId)}, mais en a {actual}.",
                    ConflictSeverity.Error,
                    ClassId: requirement.ClassId,
                    SubjectId: requirement.SubjectId));
            }
        }

        var requiredPairs = _context.Requirements
            .Select(r => (r.ClassId, r.SubjectId))
            .ToHashSet();

        foreach (var orphan in sessions
                     .Where(s => s.LessonId.HasValue && !requiredPairs.Contains((s.ClassId, s.SubjectId)))
                     .GroupBy(s => (s.ClassId, s.SubjectId)))
        {
            conflicts.Add(new ScheduleConflict(
                "HC-03",
                $"La classe {_context.ClassName(orphan.Key.ClassId)} a {orphan.Count()} séance(s) de " +
                $"{_context.SubjectName(orphan.Key.SubjectId)} sans exigence au programme.",
                ConflictSeverity.Warning,
                ClassId: orphan.Key.ClassId,
                SubjectId: orphan.Key.SubjectId));
        }
    }
}
