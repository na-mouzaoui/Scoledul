using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;
using Timetable.Domain.Common;
using Timetable.Domain.Entities;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Enseignants » : tableau, formulaire, indisponibilités (FR-07).</summary>
public sealed partial class TeachersStepViewModel : StepViewModel
{
    private readonly ProjectWorkflowService _workflow;
    private readonly IDialogService _dialogs;

    private int _editingId;

    public TeachersStepViewModel(ProjectWorkflowService workflow, IDialogService dialogs)
        : base(Strings.NavTeachers)
    {
        _workflow = workflow;
        _dialogs = dialogs;
    }

    public ObservableCollection<TeacherRowViewModel> Teachers { get; } = new();

    public ObservableCollection<TeacherRowViewModel> FilteredTeachers { get; } = new();

    public ObservableCollection<SubjectOption> Subjects { get; } = new();

    public ObservableCollection<UnavailabilitySlotViewModel> Slots { get; } = new();

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private string _formLastName = string.Empty;

    [ObservableProperty]
    private string _formFirstName = string.Empty;

    [ObservableProperty]
    private SubjectOption? _formSubject;

    [ObservableProperty]
    private TeacherRowViewModel? _selectedTeacher;

    [ObservableProperty]
    private string? _validationMessage;

    public string FormTitle => _editingId == 0 ? "Ajouter un enseignant" : "Modifier l'enseignant";

    public override void Refresh()
    {
        var snapshot = _workflow.State.Snapshot;

        Subjects.Clear();
        foreach (var subject in snapshot.Subjects.OrderBy(s => s.Name))
        {
            Subjects.Add(new SubjectOption(subject.Id, subject.Name));
        }

        Teachers.Clear();
        foreach (var teacher in snapshot.Teachers.OrderBy(t => t.LastName).ThenBy(t => t.FirstName))
        {
            Teachers.Add(new TeacherRowViewModel(
                teacher,
                snapshot.Subjects.FirstOrDefault(s => s.Id == teacher.SubjectId)?.Name ?? string.Empty));
        }

        ApplyFilter();
        BuildSlots();
        Validate();
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedTeacherChanged(TeacherRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        _editingId = value.Id;
        FormLastName = value.LastName;
        FormFirstName = value.FirstName;
        FormSubject = Subjects.FirstOrDefault(s => s.Id == value.SubjectId);
        OnPropertyChanged(nameof(FormTitle));
        RefreshUnavailabilities();
    }

    [RelayCommand]
    private void NewTeacher()
    {
        _editingId = 0;
        FormLastName = string.Empty;
        FormFirstName = string.Empty;
        FormSubject = Subjects.FirstOrDefault();
        SelectedTeacher = null;
        OnPropertyChanged(nameof(FormTitle));
        ValidationMessage = null;
    }

    [RelayCommand]
    private async Task SaveTeacher()
    {
        if (_workflow.State.Repository is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(FormLastName) || string.IsNullOrWhiteSpace(FormFirstName) || FormSubject is null)
        {
            ValidationMessage = "Le nom, le prénom et la matière sont obligatoires.";
            return;
        }

        var duplicate = Teachers.Any(t =>
            t.Id != _editingId &&
            string.Equals(t.LastName, FormLastName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.FirstName, FormFirstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            t.SubjectId == FormSubject.Id);

        if (duplicate)
        {
            ValidationMessage = "Un enseignant identique (même nom, prénom et matière) existe déjà.";
            return;
        }

        var entity = new Teacher
        {
            Id = _editingId,
            LastName = FormLastName.Trim(),
            FirstName = FormFirstName.Trim(),
            SubjectId = FormSubject.Id,
        };

        try
        {
            await _workflow.State.Repository.SaveTeacherAsync(entity);
            ValidationMessage = null;
            _editingId = 0;
            await _workflow.State.ReloadAsync();
            Refresh();
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Enregistrement impossible : {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteTeacher(TeacherRowViewModel? row)
    {
        if (row is null || _workflow.State.Repository is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Supprimer l'enseignant {row.DisplayName} ? Les séances qui lui sont affectées seront supprimées.",
                "Suppression d'un enseignant"))
        {
            return;
        }

        await _workflow.State.Repository.DeleteTeacherAsync(row.Id);
        await _workflow.State.ReloadAsync();
        NewTeacher();
        Refresh();
    }

    [RelayCommand]
    private async Task ToggleUnavailability(UnavailabilitySlotViewModel? slot)
    {
        if (slot is null || SelectedTeacher is null || _workflow.State.Repository is null)
        {
            return;
        }

        slot.IsUnavailable = !slot.IsUnavailable;
        var slotIds = Slots.Where(s => s.IsUnavailable).Select(s => s.SlotId).ToList();
        await _workflow.State.Repository.SetTeacherUnavailabilitiesAsync(SelectedTeacher.Id, slotIds);
    }

    private void ApplyFilter()
    {
        FilteredTeachers.Clear();
        foreach (var teacher in Teachers.Where(t =>
                     string.IsNullOrWhiteSpace(Filter) ||
                     t.DisplayName.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                     t.SubjectName.Contains(Filter, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredTeachers.Add(teacher);
        }
    }

    private void BuildSlots()
    {
        Slots.Clear();
        foreach (var slot in _workflow.State.Snapshot.Slots
                     .OrderBy(s => s.Weekday.DisplayIndex())
                     .ThenBy(s => s.SlotIndex))
        {
            Slots.Add(new UnavailabilitySlotViewModel(slot.Id, $"{slot.Weekday.ToFrenchLabelShort()} {slot.StartLabel}")
            {
                IsUnavailable = false,
            });
        }
    }

    private void RefreshUnavailabilities()
    {
        if (SelectedTeacher is null)
        {
            return;
        }

        var unavailable = _workflow.State.Snapshot.Teachers
            .FirstOrDefault(t => t.Id == SelectedTeacher.Id)?
            .Unavailabilities.Select(u => u.TimeSlotId).ToHashSet() ?? new HashSet<int>();

        foreach (var slot in Slots)
        {
            slot.IsUnavailable = unavailable.Contains(slot.SlotId);
        }
    }

    private void Validate()
    {
        IsValid = Teachers.Count > 0;
        if (!IsValid)
        {
            ValidationMessage = "Ajoutez au moins un enseignant.";
        }
    }
}

/// <summary>Ligne d'enseignant (FR-07).</summary>
public sealed record TeacherRowViewModel(Teacher Model, string SubjectName)
{
    public int Id => Model.Id;

    public int SubjectId => Model.SubjectId;

    public string LastName => Model.LastName;

    public string FirstName => Model.FirstName;

    public string DisplayName => Model.DisplayName;
}

/// <summary>Option de matière pour les listes déroulantes.</summary>
public sealed record SubjectOption(int Id, string Name);

/// <summary>Case d'indisponibilité d'un créneau (FR-07).</summary>
public sealed partial class UnavailabilitySlotViewModel : ObservableObject
{
    public UnavailabilitySlotViewModel(int slotId, string label)
    {
        SlotId = slotId;
        Label = label;
    }

    public int SlotId { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isUnavailable;
}
