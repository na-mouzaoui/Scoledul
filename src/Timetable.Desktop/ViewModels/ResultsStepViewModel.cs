using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Application.Models;
using Timetable.Application.Services;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;
using Timetable.Domain.Common;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Desktop.ViewModels;

/// <summary>Mode d'affichage des résultats (FR-11).</summary>
public enum ResultViewMode
{
    ByClass,
    ByTeacher,
    ByDay,
}

/// <summary>
/// Étape « Résultats » : vues par classe / enseignant / jour, édition manuelle (glisser-déposer,
/// échange), verrouillage, annuler/rétablir, panneau de conflits et statistiques (FR-11, FR-12).
/// </summary>
public sealed partial class ResultsStepViewModel : StepViewModel
{
    private const double RowHeaderWidth = 92;
    private const double MinCellWidth = 150;

    private readonly ProjectWorkflowService _workflow;
    private readonly IDialogService _dialogs;
    private readonly TimetableStatisticsService _statistics;

    private readonly List<Lesson> _lessons = new();
    private readonly EditHistory _history = new();
    private bool _loading;
    private int _versionId;

    public ResultsStepViewModel(
        ProjectWorkflowService workflow,
        IDialogService dialogs,
        TimetableStatisticsService statistics)
        : base(Strings.NavResults)
    {
        _workflow = workflow;
        _dialogs = dialogs;
        _statistics = statistics;
    }

    public ObservableCollection<OwnerOption> Owners { get; } = new();

    public ObservableCollection<ScheduleRowViewModel> Rows { get; } = new();

    public ObservableCollection<ScheduleConflict> Conflicts { get; } = new();

    public ObservableCollection<ClassStatistics> ClassStatistics { get; } = new();

    public ObservableCollection<TeacherStatistics> TeacherStatistics { get; } = new();

    public IReadOnlyList<ResultViewMode> ViewModes { get; } = new[]
    {
        ResultViewMode.ByClass, ResultViewMode.ByTeacher, ResultViewMode.ByDay,
    };

    [ObservableProperty]
    private ResultViewMode _viewMode = ResultViewMode.ByClass;

    [ObservableProperty]
    private OwnerOption? _selectedOwner;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasConflicts;

    [ObservableProperty]
    private string _versionName = "Aucune version";

    [ObservableProperty]
    private bool _hasResults;

    /// <summary>
    /// Largeur minimale de la grille (en pixels). Si elle dépasse la largeur visible, une barre de
    /// défilement horizontale apparaît au lieu de compresser les colonnes.
    /// </summary>
    [ObservableProperty]
    private double _gridMinWidth = 420;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public override void Refresh()
    {
        _loading = true;

        var snapshot = _workflow.State.Snapshot;
        var version = snapshot.SelectedVersion;

        _lessons.Clear();
        _history.Clear();

        if (version is null)
        {
            HasResults = false;
            VersionName = "Aucune version";
            Owners.Clear();
            Rows.Clear();
            Conflicts.Clear();
            StatusMessage = "Aucun emploi du temps généré pour l'instant.";
            _loading = false;
            return;
        }

        _versionId = version.Id;
        VersionName = version.Name;
        _lessons.AddRange(snapshot.LessonsForVersion(version.Id));
        HasResults = true;

        RebuildOwners();
        _loading = false;

        LoadGrid();
        UpdateConflicts();
        UpdateStatistics();
        StatusMessage = null;
    }

    partial void OnViewModeChanged(ResultViewMode value)
    {
        if (_loading)
        {
            return;
        }

        RebuildOwners();
        LoadGrid();
    }

    partial void OnSelectedOwnerChanged(OwnerOption? value)
    {
        if (!_loading)
        {
            LoadGrid();
        }
    }

    private void RebuildOwners()
    {
        var snapshot = _workflow.State.Snapshot;
        var previous = SelectedOwner?.Id ?? 0;

        _loading = true;
        Owners.Clear();

        switch (ViewMode)
        {
            case ResultViewMode.ByClass:
                foreach (var schoolClass in snapshot.Classes.OrderBy(c => c.Position))
                {
                    Owners.Add(new OwnerOption(schoolClass.Id, schoolClass.Name));
                }

                break;

            case ResultViewMode.ByTeacher:
                foreach (var teacher in snapshot.Teachers.OrderBy(t => t.LastName))
                {
                    Owners.Add(new OwnerOption(teacher.Id, teacher.DisplayName));
                }

                break;

            case ResultViewMode.ByDay:
                foreach (var day in snapshot.Days.Where(d => d.IsActive).OrderBy(d => d.Weekday.DisplayIndex()))
                {
                    Owners.Add(new OwnerOption(day.Id, day.Weekday.ToFrenchLabel()));
                }

                break;
        }

        SelectedOwner = Owners.FirstOrDefault(o => o.Id == previous) ?? Owners.FirstOrDefault();
        _loading = false;
    }

    private void LoadGrid()
    {
        Rows.Clear();

        var snapshot = _workflow.State.Snapshot;
        if (SelectedOwner is null)
        {
            UpdateGridWidth(0);
            return;
        }

        if (ViewMode == ResultViewMode.ByDay)
        {
            BuildDayGrid(snapshot, SelectedOwner.Id);
        }
        else
        {
            BuildClassOrTeacherGrid(snapshot, SelectedOwner.Id);
        }
    }

    private void BuildClassOrTeacherGrid(ProjectSnapshot snapshot, int ownerId)
    {
        var columns = snapshot.Days.Where(d => d.IsActive).OrderBy(d => d.Weekday.DisplayIndex()).ToList();
        var rows = BuildRows(snapshot, columns);
        var byTeacher = ViewMode == ResultViewMode.ByTeacher;

        UpdateGridWidth(columns.Count);

        foreach (var row in rows)
        {
            var cells = new ObservableCollection<ScheduleCellViewModel>();
            foreach (var day in columns)
            {
                cells.Add(BuildCell(snapshot, row, day, ownerId, byTeacher, day.Weekday.ToFrenchLabel()));
            }

            Rows.Add(new ScheduleRowViewModel(row, cells));
        }
    }

    private void BuildDayGrid(ProjectSnapshot snapshot, int dayId)
    {
        var day = snapshot.Days.FirstOrDefault(d => d.Id == dayId);
        if (day is null)
        {
            return;
        }

        var columns = snapshot.Classes.OrderBy(c => c.Position).ToList();
        var daySlots = snapshot.Slots.Where(s => s.DayId == dayId).OrderBy(s => s.SlotIndex).ToList();

        UpdateGridWidth(columns.Count);

        var rows = daySlots
            .Select(s => new ScheduleRow(s.StartMinute, s.EndMinute, false, string.Empty))
            .Concat(day.Breaks.Select(b => new ScheduleRow(b.StartMinute, b.EndMinute, true, b.Label)))
            .OrderBy(r => r.StartMinute)
            .ToList();

        foreach (var row in rows)
        {
            var cells = new ObservableCollection<ScheduleCellViewModel>();
            foreach (var schoolClass in columns)
            {
                cells.Add(BuildCell(snapshot, row, day, schoolClass.Id, byTeacher: false, schoolClass.Name));
            }

            Rows.Add(new ScheduleRowViewModel(row, cells));
        }
    }

    private ScheduleCellViewModel BuildCell(
        ProjectSnapshot snapshot,
        ScheduleRow row,
        SchoolDay day,
        int ownerId,
        bool byTeacher,
        string columnHeader)
    {
        var cell = new ScheduleCellViewModel { ColumnHeader = columnHeader, OwnerId = ownerId, DayId = day.Id };

        if (row.IsBreak)
        {
            cell.IsBreak = true;
            cell.Text = row.Label;
            return cell;
        }

        var slot = snapshot.Slots.FirstOrDefault(s =>
            s.DayId == day.Id && s.StartMinute == row.StartMinute && s.EndMinute == row.EndMinute);

        if (slot is null)
        {
            cell.IsUnavailable = true;
            return cell;
        }

        cell.SlotId = slot.Id;

        var lesson = _lessons.FirstOrDefault(l =>
            l.TimeSlotId == slot.Id && (byTeacher ? l.TeacherId == ownerId : l.ClassId == ownerId));

        if (lesson is not null)
        {
            cell.LessonId = lesson.Id;
            cell.IsLocked = lesson.IsLocked;
            cell.ColorHex = snapshot.Subjects.FirstOrDefault(s => s.Id == lesson.SubjectId)?.ColorHex;
            var subjectName = snapshot.Subjects.FirstOrDefault(s => s.Id == lesson.SubjectId)?.Name ?? string.Empty;
            var teacherName = snapshot.Teachers.FirstOrDefault(t => t.Id == lesson.TeacherId)?.DisplayName ?? string.Empty;
            var className = snapshot.Classes.FirstOrDefault(c => c.Id == lesson.ClassId)?.Name ?? string.Empty;

            cell.Text = ViewMode switch
            {
                ResultViewMode.ByTeacher => $"{className}\n{subjectName}",
                ResultViewMode.ByDay => $"{subjectName}\n{teacherName}",
                _ => $"{subjectName}\n{teacherName}",
            };
        }

        return cell;
    }

    private static List<ScheduleRow> BuildRows(ProjectSnapshot snapshot, IReadOnlyList<SchoolDay> days)
    {
        var rows = new List<ScheduleRow>();
        var seen = new HashSet<int>();

        foreach (var day in days)
        {
            foreach (var slot in snapshot.Slots.Where(s => s.DayId == day.Id))
            {
                if (seen.Add(slot.StartMinute))
                {
                    rows.Add(new ScheduleRow(slot.StartMinute, slot.EndMinute, false, string.Empty));
                }
            }
        }

        foreach (var day in days)
        {
            foreach (var breakPeriod in day.Breaks)
            {
                if (seen.Add(breakPeriod.StartMinute))
                {
                    rows.Add(new ScheduleRow(breakPeriod.StartMinute, breakPeriod.EndMinute, true, breakPeriod.Label));
                }
            }
        }

        return rows.OrderBy(r => r.StartMinute).ToList();
    }

    /// <summary>Déplace (ou échange) une séance vers un autre créneau, avec contrôle en temps réel (FR-12).</summary>
    public async Task<bool> TryMoveAsync(ScheduleCellViewModel? source, ScheduleCellViewModel? target)
    {
        if (source?.LessonId is null || target is null)
        {
            return false;
        }

        if (target.IsBreak || target.IsUnavailable || target.SlotId is null)
        {
            StatusMessage = "Ce créneau n'est pas disponible (pause ou hors horaires).";
            return false;
        }

        var snapshot = _workflow.State.Snapshot;
        var lesson = _lessons.FirstOrDefault(l => l.Id == source.LessonId);
        if (lesson is null)
        {
            return false;
        }

        var subject = snapshot.Subjects.FirstOrDefault(s => s.Id == lesson.SubjectId);
        var isBlock = subject?.RequiresBlocks == true;
        var moving = isBlock
            ? _lessons.Where(l => l.ClassId == lesson.ClassId && l.SubjectId == lesson.SubjectId &&
                                  l.BlockKey == lesson.BlockKey).ToList()
            : new List<Lesson> { lesson };

        var slotIndex = SlotLayout.BuildIndex(snapshot.Slots);
        var slotsById = snapshot.Slots.ToDictionary(s => s.Id);

        // Échange lorsque la case cible contient déjà une séance.
        if (target.LessonId is { } targetLessonId && moving.All(l => l.Id != targetLessonId))
        {
            var targetLesson = _lessons.FirstOrDefault(l => l.Id == targetLessonId);
            var targetSubject = snapshot.Subjects.FirstOrDefault(s => s.Id == targetLesson?.SubjectId);
            if (targetLesson is null)
            {
                return false;
            }

            if (isBlock || targetSubject?.RequiresBlocks == true)
            {
                StatusMessage = "L'échange n'est pas possible avec une matière à bloc. Utilisez un créneau libre.";
                return false;
            }

            var swapPlacements = new List<PlacementRequest>
            {
                new(lesson.ClassId, lesson.SubjectId, lesson.TeacherId, targetLesson.TimeSlotId),
                new(targetLesson.ClassId, targetLesson.SubjectId, targetLesson.TeacherId, lesson.TimeSlotId),
            };

            var swapConflicts = Validate(swapPlacements, new[] { lesson.Id, targetLesson.Id });
            if (swapConflicts.Count > 0)
            {
                StatusMessage = swapConflicts[0].Message;
                return false;
            }

            _history.Push(_lessons);
            lesson.TimeSlotId = swapPlacements[0].SlotId;
            targetLesson.TimeSlotId = swapPlacements[1].SlotId;
            await CommitAsync(swapConflicts);
            return true;
        }

        // Déplacement.
        var targetSlots = new List<int>();
        if (isBlock)
        {
            if (!slotsById.TryGetValue(target.SlotId.Value, out var start))
            {
                return false;
            }

            var blockSlots = SlotLayout.BlockSlots(start, subject!.BlockSize, slotIndex);
            if (blockSlots.Count != subject.BlockSize)
            {
                StatusMessage = $"Un bloc de {subject.BlockSize} séances ne peut pas commencer sur ce créneau.";
                return false;
            }

            targetSlots.AddRange(blockSlots.Select(s => s.Id));
        }
        else
        {
            targetSlots.Add(target.SlotId.Value);
        }

        var ordered = moving.OrderBy(l => slotsById[l.TimeSlotId].SlotIndex).ToList();
        var placements = new List<PlacementRequest>();
        for (var i = 0; i < ordered.Count && i < targetSlots.Count; i++)
        {
            placements.Add(new PlacementRequest(
                ordered[i].ClassId,
                ordered[i].SubjectId,
                ordered[i].TeacherId,
                targetSlots[i],
                ordered[i].BlockKey));
        }

        var ignoreIds = moving.Select(l => l.Id).ToArray();
        var conflicts = Validate(placements, ignoreIds);
        if (conflicts.Count > 0)
        {
            StatusMessage = conflicts[0].Message;
            return false;
        }

        _history.Push(_lessons);
        for (var i = 0; i < ordered.Count && i < targetSlots.Count; i++)
        {
            ordered[i].TimeSlotId = targetSlots[i];
        }

        await CommitAsync(conflicts);
        return true;
    }

    private List<ScheduleConflict> Validate(IReadOnlyList<PlacementRequest> placements, IEnumerable<int> ignoreIds)
    {
        var context = _workflow.State.Snapshot.ToScheduleContext();
        var validator = new ScheduleValidator(context);
        return validator
            .ValidatePlacements(_lessons, placements, ignoreIds)
            .Where(c => c.IsBlocking)
            .ToList();
    }

    private async Task CommitAsync(IReadOnlyList<ScheduleConflict> conflicts)
    {
        StatusMessage = conflicts.Count == 0 ? "Modification appliquée." : conflicts[0].Message;
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        LoadGrid();
        UpdateConflicts();
        UpdateStatistics();
        await SaveLessonsAsync();
    }

    private void UpdateConflicts()
    {
        Conflicts.Clear();
        if (!HasResults)
        {
            HasConflicts = false;
            return;
        }

        var context = _workflow.State.Snapshot.ToScheduleContext();
        var validator = new ScheduleValidator(context);
        foreach (var conflict in validator.ValidateAll(_lessons))
        {
            Conflicts.Add(conflict);
        }

        HasConflicts = Conflicts.Any(c => c.IsBlocking);
    }

    private void UpdateStatistics()
    {
        ClassStatistics.Clear();
        TeacherStatistics.Clear();
        if (!HasResults)
        {
            return;
        }

        var stats = _statistics.Compute(_workflow.State.Snapshot, _versionId);
        foreach (var item in stats.Classes)
        {
            ClassStatistics.Add(item);
        }

        foreach (var item in stats.Teachers)
        {
            TeacherStatistics.Add(item);
        }
    }

    private async Task SaveLessonsAsync(CancellationToken cancellationToken = default)
    {
        if (_workflow.State.Repository is not null)
        {
            await _workflow.State.Repository.SaveLessonsAsync(_versionId, _lessons, cancellationToken);
            _workflow.State.NotifySaved();
        }
    }

    [RelayCommand]
    private async Task ToggleLock(ScheduleCellViewModel? cell)
    {
        if (cell?.LessonId is null)
        {
            return;
        }

        var lesson = _lessons.FirstOrDefault(l => l.Id == cell.LessonId);
        if (lesson is null)
        {
            return;
        }

        _history.Push(_lessons);
        var isBlock = _workflow.State.Snapshot.Subjects
            .FirstOrDefault(s => s.Id == lesson.SubjectId)?.RequiresBlocks == true;

        var targets = isBlock
            ? _lessons.Where(l => l.BlockKey == lesson.BlockKey && l.ClassId == lesson.ClassId).ToList()
            : new List<Lesson> { lesson };

        var newValue = !lesson.IsLocked;
        foreach (var target in targets)
        {
            target.IsLocked = newValue;
        }

        await CommitAsync(Array.Empty<ScheduleConflict>());
    }

    [RelayCommand]
    private async Task Undo()
    {
        var state = _history.Undo(_lessons);
        if (state is null)
        {
            return;
        }

        ReplaceLessons(state);
        await CommitAsync(Array.Empty<ScheduleConflict>());
    }

    [RelayCommand]
    private async Task Redo()
    {
        var state = _history.Redo(_lessons);
        if (state is null)
        {
            return;
        }

        ReplaceLessons(state);
        await CommitAsync(Array.Empty<ScheduleConflict>());
    }

    private void ReplaceLessons(IReadOnlyList<Lesson> lessons)
    {
        _lessons.Clear();
        _lessons.AddRange(lessons);
    }

    private void UpdateGridWidth(int columnCount)
        => GridMinWidth = columnCount <= 0 ? 420 : RowHeaderWidth + (columnCount * MinCellWidth);

    [RelayCommand]
    private async Task ValidateSchedule()
    {
        UpdateConflicts();
        if (HasConflicts)
        {
            _dialogs.ShowWarning(
                "Des conflits subsistent. Corrigez-les avant de valider l'emploi du temps.",
                "Validation impossible");
            return;
        }

        await _workflow.State.Repository!.SetVersionSelectedAsync(_versionId);
        _dialogs.ShowInformation($"L'emploi du temps « {VersionName} » est validé et enregistré.");
    }
}

/// <summary>Ligne de la grille des résultats (créneau ou pause).</summary>
public sealed record ScheduleRow(int StartMinute, int EndMinute, bool IsBreak, string Label)
{
    public string TimeLabel => $"{MinuteOfDay.Format(StartMinute)}–{MinuteOfDay.Format(EndMinute)}";
}

/// <summary>Ligne de la grille des résultats.</summary>
public sealed class ScheduleRowViewModel
{
    public ScheduleRowViewModel(ScheduleRow row, ObservableCollection<ScheduleCellViewModel> cells)
    {
        Row = row;
        Cells = cells;
    }

    public ScheduleRow Row { get; }

    public ObservableCollection<ScheduleCellViewModel> Cells { get; }

    public string Header => Row.IsBreak ? Row.Label : Row.TimeLabel;

    public bool IsBreak => Row.IsBreak;
}

/// <summary>Case de la grille des résultats.</summary>
public sealed partial class ScheduleCellViewModel : ObservableObject
{
    [ObservableProperty]
    private int? _slotId;

    [ObservableProperty]
    private int? _lessonId;

    [ObservableProperty]
    private int _ownerId;

    [ObservableProperty]
    private int _dayId;

    [ObservableProperty]
    private string? _text;

    [ObservableProperty]
    private string? _colorHex;

    [ObservableProperty]
    private string _columnHeader = string.Empty;

    [ObservableProperty]
    private bool _isBreak;

    [ObservableProperty]
    private bool _isUnavailable;

    [ObservableProperty]
    private bool _isLocked;

    public bool HasLesson => LessonId.HasValue;
}
