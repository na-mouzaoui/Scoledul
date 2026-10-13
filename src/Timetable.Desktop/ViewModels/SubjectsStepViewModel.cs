using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;
using Timetable.Domain.Entities;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Matières » : nom, couleur, taille de bloc (FR-06).</summary>
public sealed partial class SubjectsStepViewModel : StepViewModel
{
    private readonly ProjectWorkflowService _workflow;
    private readonly IDialogService _dialogs;

    public SubjectsStepViewModel(ProjectWorkflowService workflow, IDialogService dialogs)
        : base(Strings.NavSubjects)
    {
        _workflow = workflow;
        _dialogs = dialogs;
    }

    public ObservableCollection<SubjectRowViewModel> Subjects { get; } = new();

    public IReadOnlyList<int> BlockSizes { get; } = new[] { 1, 2, 3, 4 };

    [ObservableProperty]
    private string? _validationMessage;

    public override void Refresh()
    {
        Subjects.Clear();
        foreach (var subject in _workflow.State.Snapshot.Subjects.OrderBy(s => s.Id))
        {
            Subjects.Add(new SubjectRowViewModel(subject, OnSubjectChanged));
        }

        Validate();
    }

    [RelayCommand]
    private async Task AddSubject()
    {
        if (_workflow.State.Repository is null)
        {
            return;
        }

        var name = "Nouvelle matière";
        var index = 2;
        while (Subjects.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"Nouvelle matière {index++}";
        }

        var saved = await _workflow.State.Repository.SaveSubjectAsync(new Subject
        {
            Name = name,
            ColorHex = "#4F81BD",
            BlockSize = 1,
        });

        Subjects.Add(new SubjectRowViewModel(saved, OnSubjectChanged));
        await _workflow.State.ReloadAsync();
        Validate();
    }

    [RelayCommand]
    private async Task RemoveSubject(SubjectRowViewModel? row)
    {
        if (row is null || _workflow.State.Repository is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Supprimer la matière « {row.Name} » ? Les enseignants et le programme associés seront concernés.",
                "Suppression d'une matière"))
        {
            return;
        }

        try
        {
            await _workflow.State.Repository.DeleteSubjectAsync(row.Id);
            Subjects.Remove(row);
            await _workflow.State.ReloadAsync();
            Refresh();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(
                $"Impossible de supprimer « {row.Name} » : des enseignants ou des exigences y sont rattachés.\n{ex.Message}");
        }
    }

    private void OnSubjectChanged(SubjectRowViewModel row)
    {
        if (!Validate())
        {
            return;
        }

        _workflow.QueueSave(async ct =>
        {
            if (_workflow.State.Repository is null)
            {
                return;
            }

            var saved = await _workflow.State.Repository.SaveSubjectAsync(row.ToModel(), ct);
            row.Id = saved.Id;
            await _workflow.State.ReloadAsync(ct);
        });
    }

    private bool Validate()
    {
        if (Subjects.Count == 0)
        {
            ValidationMessage = "Ajoutez au moins une matière.";
            IsValid = false;
            return false;
        }

        if (Subjects.Any(s => string.IsNullOrWhiteSpace(s.Name)))
        {
            ValidationMessage = "Le nom d'une matière ne peut pas être vide.";
            IsValid = false;
            return false;
        }

        var duplicate = Subjects
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            ValidationMessage = $"La matière « {duplicate.Key} » existe en double.";
            IsValid = false;
            return false;
        }

        ValidationMessage = null;
        IsValid = true;
        return true;
    }
}

/// <summary>Ligne d'une matière (FR-06).</summary>
public sealed partial class SubjectRowViewModel : ObservableObject
{
    private readonly Action<SubjectRowViewModel> _onChanged;

    public SubjectRowViewModel(Subject model, Action<SubjectRowViewModel> onChanged)
    {
        Id = model.Id;
        _name = model.Name;
        _colorHex = model.ColorHex;
        _blockSize = model.BlockSize;
        _onChanged = onChanged;
    }

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _colorHex;

    [ObservableProperty]
    private int _blockSize;

    public bool RequiresBlocks => BlockSize > 1;

    partial void OnNameChanged(string value) => _onChanged(this);

    partial void OnColorHexChanged(string value) => _onChanged(this);

    partial void OnBlockSizeChanged(int value)
    {
        OnPropertyChanged(nameof(RequiresBlocks));
        _onChanged(this);
    }

    public Subject ToModel() => new()
    {
        Id = Id,
        Name = Name.Trim(),
        ColorHex = string.IsNullOrWhiteSpace(ColorHex) ? "#4F81BD" : ColorHex.Trim(),
        BlockSize = Math.Clamp(BlockSize, Subject.MinBlockSize, Subject.MaxBlockSize),
    };
}
