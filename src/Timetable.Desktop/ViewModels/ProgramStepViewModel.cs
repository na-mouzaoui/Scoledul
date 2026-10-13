using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;
using Timetable.Domain.Entities;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Programme » : séances hebdomadaires par classe et matière, avec indicateur de charge (FR-08).</summary>
public sealed partial class ProgramStepViewModel : StepViewModel
{
    private readonly ProjectWorkflowService _workflow;
    private readonly IDialogService _dialogs;

    private bool _loading;

    public ProgramStepViewModel(ProjectWorkflowService workflow, IDialogService dialogs)
        : base(Strings.NavProgram)
    {
        _workflow = workflow;
        _dialogs = dialogs;
    }

    public ObservableCollection<OwnerOption> Classes { get; } = new();

    public ObservableCollection<ProgramRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private OwnerOption? _selectedClass;

    [ObservableProperty]
    private int _totalSessions;

    [ObservableProperty]
    private int _availableSlots;

    [ObservableProperty]
    private bool _isOverloaded;

    [ObservableProperty]
    private string? _validationMessage;

    public override void Refresh()
    {
        _loading = true;
        var snapshot = _workflow.State.Snapshot;

        var previous = SelectedClass?.Id ?? 0;
        Classes.Clear();
        foreach (var schoolClass in snapshot.Classes.OrderBy(c => c.Position))
        {
            Classes.Add(new OwnerOption(schoolClass.Id, schoolClass.Name));
        }

        SelectedClass = Classes.FirstOrDefault(c => c.Id == previous) ?? Classes.FirstOrDefault();
        _loading = false;

        LoadRows();
    }

    partial void OnSelectedClassChanged(OwnerOption? value)
    {
        if (!_loading)
        {
            LoadRows();
        }
    }

    private void LoadRows()
    {
        _loading = true;
        Rows.Clear();

        var snapshot = _workflow.State.Snapshot;
        if (SelectedClass is not null)
        {
            foreach (var subject in snapshot.Subjects.OrderBy(s => s.Name))
            {
                var requirement = snapshot.Requirements
                    .FirstOrDefault(r => r.ClassId == SelectedClass.Id && r.SubjectId == subject.Id);

                var teachers = new List<OwnerOption> { new(0, "(au choix du solveur)") };
                teachers.AddRange(snapshot.Teachers
                    .Where(t => t.SubjectId == subject.Id)
                    .Select(t => new OwnerOption(t.Id, t.DisplayName)));

                Rows.Add(new ProgramRowViewModel(
                    subject,
                    requirement?.WeeklySessions ?? 0,
                    requirement?.FixedTeacherId ?? 0,
                    teachers,
                    OnRowChanged));
            }
        }

        _loading = false;
        UpdateLoad();
    }

    private void OnRowChanged(ProgramRowViewModel row)
    {
        if (_loading || SelectedClass is null)
        {
            return;
        }

        Validate();
        UpdateLoad();
        if (!IsValid)
        {
            return;
        }

        // La classe et les valeurs sont figées ici : si l'utilisateur change de classe avant la
        // fin de l'enregistrement automatique, ce sont bien les bonnes données qui sont persistées.
        var classId = SelectedClass.Id;
        var requirements = BuildRequirements(classId);
        _workflow.QueueSave(ct => SaveRequirementsAsync(classId, requirements, ct));
    }

    private List<ClassSubjectRequirement> BuildRequirements(int classId)
        => Rows.Select(r => new ClassSubjectRequirement
        {
            ClassId = classId,
            SubjectId = r.SubjectId,
            WeeklySessions = r.WeeklySessions,
            FixedTeacherId = r.FixedTeacherId == 0 ? null : r.FixedTeacherId,
        }).ToList();

    private async Task SaveRequirementsAsync(
        int classId,
        IReadOnlyList<ClassSubjectRequirement> requirements,
        CancellationToken cancellationToken)
    {
        if (_workflow.State.Repository is null)
        {
            return;
        }

        await _workflow.State.Repository.SaveRequirementsAsync(classId, requirements, cancellationToken);
        await _workflow.State.ReloadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task CopyToOtherClasses()
    {
        if (SelectedClass is null || _workflow.State.Repository is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Copier le programme de « {SelectedClass.Name} » vers toutes les autres classes ? " +
                "Les programmes existants seront remplacés.",
                "Copier le programme"))
        {
            return;
        }

        foreach (var target in Classes.Where(c => c.Id != SelectedClass.Id))
        {
            var requirements = Rows.Select(r => new ClassSubjectRequirement
            {
                ClassId = target.Id,
                SubjectId = r.SubjectId,
                WeeklySessions = r.WeeklySessions,
                FixedTeacherId = r.FixedTeacherId == 0 ? null : r.FixedTeacherId,
            }).ToList();

            await _workflow.State.Repository.SaveRequirementsAsync(target.Id, requirements);
        }

        await _workflow.State.ReloadAsync();
        Refresh();
    }

    public override Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedClass is null)
        {
            return Task.CompletedTask;
        }

        var classId = SelectedClass.Id;
        return SaveRequirementsAsync(classId, BuildRequirements(classId), cancellationToken);
    }

    private void Validate()
    {
        foreach (var row in Rows)
        {
            if (row.WeeklySessions < 0)
            {
                ValidationMessage = $"Le nombre de séances de {row.SubjectName} ne peut pas être négatif.";
                IsValid = false;
                return;
            }

            if (row.RequiresBlocks && row.WeeklySessions % row.BlockSize != 0)
            {
                ValidationMessage =
                    $"{row.SubjectName} exige des blocs de {row.BlockSize} séances : {row.WeeklySessions} n'est pas un multiple.";
                IsValid = false;
                return;
            }
        }

        ValidationMessage = null;
        IsValid = true;
    }

    private void UpdateLoad()
    {
        TotalSessions = Rows.Sum(r => r.WeeklySessions);
        AvailableSlots = _workflow.State.Snapshot.Slots.Count;
        IsOverloaded = TotalSessions > AvailableSlots;
        if (IsOverloaded)
        {
            ValidationMessage =
                $"Charge trop élevée : {TotalSessions} séances pour {AvailableSlots} créneaux disponibles.";
            IsValid = false;
        }
        else if (Rows.Count > 0 && TotalSessions == 0)
        {
            ValidationMessage =
                "Aucune séance définie pour cette classe : saisissez le nombre de séances par matière.";
            IsValid = true;
        }
        else if (Rows.Count > 0)
        {
            IsValid = true;
        }
    }
}

/// <summary>Ligne du programme d'une classe (FR-08).</summary>
public sealed partial class ProgramRowViewModel : ObservableObject
{
    private readonly Action<ProgramRowViewModel> _onChanged;

    public ProgramRowViewModel(
        Subject subject,
        int weeklySessions,
        int fixedTeacherId,
        IReadOnlyList<OwnerOption> teachers,
        Action<ProgramRowViewModel> onChanged)
    {
        SubjectId = subject.Id;
        SubjectName = subject.Name;
        BlockSize = subject.BlockSize;
        Teachers = teachers;
        _weeklySessions = weeklySessions;
        _fixedTeacherId = fixedTeacherId;
        _onChanged = onChanged;
    }

    public int SubjectId { get; }

    public string SubjectName { get; }

    public int BlockSize { get; }

    public bool RequiresBlocks => BlockSize > 1;

    public IReadOnlyList<OwnerOption> Teachers { get; }

    [ObservableProperty]
    private int _weeklySessions;

    [ObservableProperty]
    private int _fixedTeacherId;

    partial void OnWeeklySessionsChanged(int value) => _onChanged(this);

    partial void OnFixedTeacherIdChanged(int value) => _onChanged(this);
}

/// <summary>Option générique (classe, enseignant) pour les listes déroulantes.</summary>
public sealed record OwnerOption(int Id, string Name);
