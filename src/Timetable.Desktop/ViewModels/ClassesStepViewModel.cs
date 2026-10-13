using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;
using Timetable.Domain.Entities;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Classes » : nombre de classes, renommage, ajout/suppression (FR-05).</summary>
public sealed partial class ClassesStepViewModel : StepViewModel
{
    public const int MinClasses = 1;
    public const int MaxClasses = 60;

    private readonly ProjectWorkflowService _workflow;
    private readonly IDialogService _dialogs;

    public ClassesStepViewModel(ProjectWorkflowService workflow, IDialogService dialogs)
        : base(Strings.NavClasses)
    {
        _workflow = workflow;
        _dialogs = dialogs;
    }

    public ObservableCollection<ClassRowViewModel> Classes { get; } = new();

    [ObservableProperty]
    private int _numberToAdd = 1;

    [ObservableProperty]
    private string? _validationMessage;

    public override void Refresh()
    {
        Classes.Clear();
        foreach (var schoolClass in _workflow.State.Snapshot.Classes.OrderBy(c => c.Position))
        {
            Classes.Add(new ClassRowViewModel(schoolClass, OnNameChanged));
        }

        Validate();
    }

    [RelayCommand]
    private async Task AddClasses()
    {
        var count = Math.Clamp(NumberToAdd, 1, MaxClasses);
        if (Classes.Count + count > MaxClasses)
        {
            _dialogs.ShowWarning($"Le nombre maximal de classes est {MaxClasses}.");
            count = MaxClasses - Classes.Count;
        }

        if (count <= 0 || _workflow.State.Repository is null)
        {
            return;
        }

        var position = Classes.Count == 0 ? 0 : Classes.Max(c => c.Position) + 1;
        for (var i = 0; i < count; i++)
        {
            var entity = SchoolClass.WithDefaultName(position + i);
            while (Classes.Any(c => string.Equals(c.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                position++;
                entity = SchoolClass.WithDefaultName(position + i);
            }

            var saved = await _workflow.State.Repository.SaveClassAsync(entity);
            Classes.Add(new ClassRowViewModel(saved, OnNameChanged));
        }

        await _workflow.State.ReloadAsync();
        Refresh();
    }

    [RelayCommand]
    private async Task RemoveClass(ClassRowViewModel? row)
    {
        if (row is null || _workflow.State.Repository is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Supprimer la classe « {row.Name} » ? Les séances et le programme associés seront supprimés.",
                "Suppression d'une classe"))
        {
            return;
        }

        await _workflow.State.Repository.DeleteClassAsync(row.Id);
        Classes.Remove(row);
        await _workflow.State.ReloadAsync();
        Refresh();
    }

    private void OnNameChanged(ClassRowViewModel row)
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

            var saved = await _workflow.State.Repository.SaveClassAsync(row.ToModel(), ct);
            row.Id = saved.Id;
        });
    }

    private bool Validate()
    {
        var names = Classes.Select(c => c.Name.Trim()).ToList();
        var duplicate = names
            .Where(n => !string.IsNullOrEmpty(n))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (Classes.Count == 0)
        {
            ValidationMessage = "Ajoutez au moins une classe.";
            IsValid = false;
            return false;
        }

        if (names.Any(string.IsNullOrWhiteSpace))
        {
            ValidationMessage = "Le nom d'une classe ne peut pas être vide.";
            IsValid = false;
            return false;
        }

        if (duplicate is not null)
        {
            ValidationMessage = $"Le nom « {duplicate.Key} » est utilisé par plusieurs classes.";
            IsValid = false;
            return false;
        }

        ValidationMessage = null;
        IsValid = true;
        return true;
    }
}

/// <summary>Ligne d'une classe (FR-05).</summary>
public sealed partial class ClassRowViewModel : ObservableObject
{
    private readonly Action<ClassRowViewModel> _onChanged;

    public ClassRowViewModel(SchoolClass model, Action<ClassRowViewModel> onChanged)
    {
        Id = model.Id;
        Position = model.Position;
        _name = model.Name;
        _onChanged = onChanged;
    }

    [ObservableProperty]
    private int _id;

    public int Position { get; set; }

    [ObservableProperty]
    private string _name;

    partial void OnNameChanged(string value) => _onChanged(this);

    public SchoolClass ToModel() => new() { Id = Id, Name = Name.Trim(), Position = Position };
}
