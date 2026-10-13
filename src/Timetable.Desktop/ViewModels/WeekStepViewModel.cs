using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;
using Timetable.Domain.Common;
using Timetable.Domain.Entities;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Semaine » : jours actifs, horaires, durée des créneaux, pauses et aperçu en direct (FR-02, FR-03, FR-04).</summary>
public sealed partial class WeekStepViewModel : StepViewModel
{
    private static readonly int[] StandardDurations = { 30, 45, 50, 55, 60, 90 };

    private readonly ProjectWorkflowService _workflow;
    private readonly ISlotGenerator _slotGenerator;
    private readonly IDialogService _dialogs;
    private readonly IAppSettingsService _appSettings;
    private readonly IProjectFileService _projectFiles;

    private bool _loading;

    public WeekStepViewModel(
        ProjectWorkflowService workflow,
        ISlotGenerator slotGenerator,
        IDialogService dialogs,
        IAppSettingsService appSettings,
        IProjectFileService projectFiles)
        : base(Strings.NavWeek)
    {
        _workflow = workflow;
        _slotGenerator = slotGenerator;
        _dialogs = dialogs;
        _appSettings = appSettings;
        _projectFiles = projectFiles;
    }

    public ObservableCollection<DayRowViewModel> Days { get; } = new();

    public ObservableCollection<DayPreviewViewModel> Preview { get; } = new();

    public ObservableCollection<string> Warnings { get; } = new();

    public IReadOnlyList<int> DurationOptions { get; } = StandardDurations;

    [ObservableProperty]
    private int _slotDurationMinutes = 60;

    [ObservableProperty]
    private int _totalSlotCount;

    public override void Refresh()
    {
        _loading = true;
        Days.Clear();

        var snapshot = _workflow.State.Snapshot;
        foreach (var weekday in WeekdayExtensions.DisplayOrder)
        {
            var day = snapshot.Days.FirstOrDefault(d => d.Weekday == weekday)
                      ?? new SchoolDay { Weekday = weekday, IsActive = false, StartMinute = 8 * 60, EndMinute = 16 * 60 };
            Days.Add(new DayRowViewModel(day, OnDayChanged));
        }

        SlotDurationMinutes = snapshot.SlotDurationMinutes <= 0 ? 60 : snapshot.SlotDurationMinutes;
        _loading = false;
        Rebuild();
    }

    partial void OnSlotDurationMinutesChanged(int value)
    {
        if (!_loading)
        {
            Rebuild();
        }
    }

    private void OnDayChanged() => Rebuild();

    /// <summary>Mémorise la configuration courante comme configuration par défaut des nouveaux projets.</summary>
    [RelayCommand]
    private void SaveAsDefault()
    {
        _appSettings.Current.GlobalWeek = BuildWeekConfiguration();
        _appSettings.Save();
        _dialogs.ShowInformation(
            "Configuration mémorisée. Elle sera appliquée automatiquement à tout nouveau projet.",
            "Configuration par défaut");
    }

    /// <summary>Applique la configuration de la semaine à tous les projets récents sans emploi du temps généré.</summary>
    [RelayCommand]
    private async Task ApplyToAllProjects()
    {
        if (_workflow.State.Repository is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Appliquer cette configuration (jours, horaires, pauses, durée) à tous les projets récents ?\n\n" +
                "Les projets contenant déjà un emploi du temps généré sont ignorés pour ne pas le perdre.",
                "Appliquer à tous les projets"))
        {
            return;
        }

        var days = Days.Select(d => d.ToModel()).ToList();
        var slots = days.Count > 0
            ? _slotGenerator.Generate(days, SlotDurationMinutes).Slots.ToList()
            : new List<TimeSlot>();

        var currentPath = _workflow.State.ProjectPath;
        var targets = _projectFiles.GetRecentProjects()
            .Where(r => !string.Equals(r.FilePath, currentPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var target in targets)
        {
            try
            {
                var repository = await _projectFiles.OpenAsync(target.FilePath);
                try
                {
                    var snapshot = await repository.LoadAsync();
                    if (snapshot.Lessons.Count > 0)
                    {
                        skipped++;
                        continue;
                    }

                    await repository.SaveWeekAsync(days, slots);
                    updated++;
                }
                finally
                {
                    await repository.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        _appSettings.Current.GlobalWeek = BuildWeekConfiguration();
        _appSettings.Save();

        _dialogs.ShowInformation(
            $"{updated} projet(s) mis à jour, {skipped} ignoré(s) (emploi du temps existant), {failed} échec(s).\n\n" +
            "Cette configuration est aussi devenue la configuration par défaut des nouveaux projets.",
            "Appliquer à tous les projets");
    }

    private WeekConfiguration BuildWeekConfiguration()
        => WeekConfigurationMapper.From(Days.Select(d => d.ToModel()).ToList(), SlotDurationMinutes);

    [RelayCommand]
    private void AddBreak(DayRowViewModel? day)
    {
        day ??= Days.FirstOrDefault(d => d.IsActive);
        if (day is null)
        {
            return;
        }

        var start = day.EndMinuteValue > 12 * 60 ? 12 * 60 : day.StartMinuteValue + 60;
        day.AddBreak("Pause", start, Math.Min(start + 15, day.EndMinuteValue));
        Rebuild();
    }

    [RelayCommand]
    private void RemoveBreak(BreakRowViewModel? breakRow)
    {
        if (breakRow is null)
        {
            return;
        }

        FindDay(breakRow)?.Breaks.Remove(breakRow);
        Rebuild();
    }

    [RelayCommand]
    private void ApplyBreakToAllDays(BreakRowViewModel? breakRow)
    {
        if (breakRow is null)
        {
            return;
        }

        var source = FindDay(breakRow);
        if (source is null)
        {
            return;
        }

        foreach (var day in Days.Where(d => d.IsActive && d != source))
        {
            if (breakRow.StartMinuteValue >= day.StartMinuteValue && breakRow.EndMinuteValue <= day.EndMinuteValue)
            {
                day.AddBreak(breakRow.Label, breakRow.StartMinuteValue, breakRow.EndMinuteValue);
            }
        }

        Rebuild();
    }

    private DayRowViewModel? FindDay(BreakRowViewModel breakRow)
        => Days.FirstOrDefault(d => d.Breaks.Contains(breakRow));

    private void Rebuild()
    {
        Preview.Clear();
        Warnings.Clear();

        var activeDays = Days.Where(d => d.IsActive).ToList();
        var validationErrors = new List<string>();

        if (activeDays.Count == 0)
        {
            validationErrors.Add("Au moins un jour actif est nécessaire.");
        }

        foreach (var day in activeDays)
        {
            if (day.EndMinuteValue <= day.StartMinuteValue)
            {
                validationErrors.Add($"{day.Label} : l'heure de fin doit être postérieure à l'heure de début.");
                continue;
            }

            foreach (var breakRow in day.Breaks)
            {
                if (breakRow.EndMinuteValue <= breakRow.StartMinuteValue)
                {
                    validationErrors.Add($"{day.Label} : la pause « {breakRow.Label} » a des horaires invalides.");
                }
                else if (breakRow.StartMinuteValue < day.StartMinuteValue || breakRow.EndMinuteValue > day.EndMinuteValue)
                {
                    validationErrors.Add($"{day.Label} : la pause « {breakRow.Label} » est en dehors des horaires du jour.");
                }
            }

            var ordered = day.Breaks.OrderBy(b => b.StartMinuteValue).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartMinuteValue < ordered[i - 1].EndMinuteValue)
                {
                    validationErrors.Add($"{day.Label} : deux pauses se chevauchent.");
                }
            }

            if (day.Breaks.Sum(b => b.Duration) >= day.EndMinuteValue - day.StartMinuteValue)
            {
                validationErrors.Add($"{day.Label} : les pauses couvrent toute la journée.");
            }
        }

        var model = activeDays.Select(d => d.ToModel()).ToList();
        if (model.Count > 0 && SlotDurationMinutes > 0)
        {
            var result = _slotGenerator.Generate(model, SlotDurationMinutes);
            TotalSlotCount = result.Slots.Count;

            foreach (var warning in result.Warnings)
            {
                Warnings.Add(
                    $"{warning.Weekday.ToFrenchLabel()} : {warning.ResidualMinutes} minute(s) résiduelle(s) ignorée(s) " +
                    $"à partir de {MinuteOfDay.Format(warning.StartMinute)}.");
            }

            foreach (var day in activeDays)
            {
                Preview.Add(DayPreviewViewModel.Build(day, result.Slots.Where(s => s.DayId == day.DayId || s.Day?.Weekday == day.Weekday)));
            }

            if (result.Slots.Count == 0)
            {
                validationErrors.Add("Aucun créneau généré : vérifiez la durée et les horaires.");
            }
        }

        foreach (var error in validationErrors)
        {
            Warnings.Add(error);
        }

        IsValid = validationErrors.Count == 0 && activeDays.Count > 0;

        if (IsValid && _workflow.State.IsProjectOpen)
        {
            _workflow.QueueSave(SaveAsync);
        }
    }

    public override async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_workflow.State.Repository is null)
        {
            return;
        }

        var activeDays = Days.Where(d => d.IsActive).ToList();
        var model = activeDays.Select(d => d.ToModel()).ToList();
        var slots = activeDays.Count > 0
            ? _slotGenerator.Generate(model, SlotDurationMinutes).Slots.ToList()
            : new List<TimeSlot>();

        await _workflow.State.Repository.SaveWeekAsync(model, slots, cancellationToken);
        await _workflow.State.ReloadAsync(cancellationToken);
    }
}

/// <summary>Ligne de configuration d'un jour (FR-02).</summary>
public sealed partial class DayRowViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private readonly int _dayId;

    public DayRowViewModel(SchoolDay day, Action onChanged)
    {
        _dayId = day.Id;
        Weekday = day.Weekday;
        _onChanged = onChanged;
        _isActive = day.IsActive;
        _startText = MinuteOfDay.Format(day.StartMinute == 0 ? 8 * 60 : day.StartMinute);
        _endText = MinuteOfDay.Format(day.EndMinute == 0 ? 16 * 60 : day.EndMinute);
        foreach (var breakPeriod in day.Breaks.OrderBy(b => b.StartMinute))
        {
            Breaks.Add(new BreakRowViewModel(breakPeriod, onChanged));
        }
    }

    public Weekday Weekday { get; }

    public string Label => Weekday.ToFrenchLabel();

    public int DayId => _dayId;

    public ObservableCollection<BreakRowViewModel> Breaks { get; } = new();

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _startText = "08:00";

    [ObservableProperty]
    private string _endText = "16:00";

    public int StartMinuteValue => MinuteOfDay.TryParse(StartText, out var value) ? value : -1;

    public int EndMinuteValue => MinuteOfDay.TryParse(EndText, out var value) ? value : -1;

    partial void OnIsActiveChanged(bool value) => _onChanged();

    partial void OnStartTextChanged(string value) => _onChanged();

    partial void OnEndTextChanged(string value) => _onChanged();

    public void AddBreak(string label, int start, int end)
    {
        Breaks.Add(new BreakRowViewModel(
            new BreakPeriod { Label = label, StartMinute = start, EndMinute = end },
            _onChanged));
    }

    public SchoolDay ToModel() => new()
    {
        Id = _dayId,
        Weekday = Weekday,
        IsActive = IsActive,
        StartMinute = Math.Max(0, StartMinuteValue),
        EndMinute = Math.Min(MinuteOfDay.MinutesPerDay, EndMinuteValue < 0 ? Math.Max(0, StartMinuteValue) : EndMinuteValue),
        Breaks = Breaks.Select(b => b.ToModel()).ToList(),
    };
}

/// <summary>Ligne d'une pause (FR-03).</summary>
public sealed partial class BreakRowViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public BreakRowViewModel(BreakPeriod breakPeriod, Action onChanged)
    {
        _onChanged = onChanged;
        _label = breakPeriod.Label;
        _startText = MinuteOfDay.Format(breakPeriod.StartMinute);
        _endText = MinuteOfDay.Format(breakPeriod.EndMinute);
    }

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _startText;

    [ObservableProperty]
    private string _endText;

    public int StartMinuteValue => MinuteOfDay.TryParse(StartText, out var value) ? value : -1;

    public int EndMinuteValue => MinuteOfDay.TryParse(EndText, out var value) ? value : -1;

    public int Duration => Math.Max(0, EndMinuteValue - StartMinuteValue);

    partial void OnLabelChanged(string value) => _onChanged();

    partial void OnStartTextChanged(string value) => _onChanged();

    partial void OnEndTextChanged(string value) => _onChanged();

    public BreakPeriod ToModel() => new()
    {
        Label = Label,
        StartMinute = Math.Max(0, StartMinuteValue),
        EndMinute = Math.Max(0, EndMinuteValue),
    };
}

/// <summary>Aperçu de la grille d'un jour (FR-04 : mise à jour en direct).</summary>
public sealed class DayPreviewViewModel
{
    public required string DayLabel { get; init; }

    public required IReadOnlyList<PreviewItem> Items { get; init; }

    public static DayPreviewViewModel Build(DayRowViewModel day, IEnumerable<TimeSlot> slots)
    {
        var items = new List<PreviewItem>();
        var breaks = day.Breaks
            .Where(b => b.StartMinuteValue >= 0 && b.EndMinuteValue > b.StartMinuteValue)
            .Select(b => (Start: b.StartMinuteValue, End: b.EndMinuteValue, Label: b.Label))
            .ToList();

        foreach (var slot in slots.OrderBy(s => s.SlotIndex))
        {
            items.Add(new PreviewItem(
                $"{MinuteOfDay.Format(slot.StartMinute)} – {MinuteOfDay.Format(slot.EndMinute)}",
                false));

            var nextBreak = breaks.FirstOrDefault(b => b.Start == slot.EndMinute);
            if (nextBreak.Label is not null)
            {
                items.Add(new PreviewItem(
                    $"Pause · {nextBreak.Label} ({MinuteOfDay.Format(nextBreak.Start)} – {MinuteOfDay.Format(nextBreak.End)})",
                    true));
            }
        }

        return new DayPreviewViewModel { DayLabel = day.Label, Items = items };
    }
}

/// <summary>Élément de l'aperçu (créneau ou pause).</summary>
public sealed record PreviewItem(string Text, bool IsBreak);
