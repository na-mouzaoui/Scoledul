using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Desktop.Services;

namespace Timetable.Desktop.ViewModels;

/// <summary>
/// Fenêtre principale : navigation par étapes, barre de statut et commandes de fichier (§11).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ProjectWorkflowService _workflow;
    private readonly AppState _state;
    private readonly ResultsStepViewModel _results;

    public ShellViewModel(
        ProjectStepViewModel project,
        WeekStepViewModel week,
        ClassesStepViewModel classes,
        SubjectsStepViewModel subjects,
        TeachersStepViewModel teachers,
        ProgramStepViewModel program,
        GenerationStepViewModel generation,
        ResultsStepViewModel results,
        ExportsStepViewModel exports,
        SettingsStepViewModel settings,
        ProjectWorkflowService workflow,
        AppState state)
    {
        _workflow = workflow;
        _state = state;
        _results = results;

        Steps = new ObservableCollection<StepViewModel>
        {
            project, week, classes, subjects, teachers, program, generation, results, exports, settings,
        };

        _selectedStep = Steps[0];

        AssignIcons(project, week, classes, subjects, teachers, program, generation, results, exports, settings);

        generation.GenerationCompleted += async (_, _) => await NavigateToAsync(results);
        workflow.ProjectChanged += async (_, _) => await ReloadAndRefreshAsync();
        state.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(AppState.ProjectName):
                    OnPropertyChanged(nameof(ProjectName));
                    break;
                case nameof(AppState.SaveStatus):
                    OnPropertyChanged(nameof(SaveStatus));
                    break;
                case nameof(AppState.IsProjectOpen):
                    OnPropertyChanged(nameof(IsProjectOpen));
                    break;
                case nameof(AppState.IsDirty):
                    OnPropertyChanged(nameof(DirtyIndicator));
                    break;
            }
        };
    }

    public ObservableCollection<StepViewModel> Steps { get; }

    [ObservableProperty]
    private StepViewModel _selectedStep;

    [ObservableProperty]
    private string _lastGeneration = "—";

    public string ProjectName => _state.ProjectName;

    public string SaveStatus => _state.SaveStatus;

    public bool IsProjectOpen => _state.IsProjectOpen;

    public string DirtyIndicator => _state.IsDirty ? "●" : "○";

    public bool CanGoNext => SelectedStep.IsValid && Steps.IndexOf(SelectedStep) < Steps.Count - 1;

    public bool CanGoPrevious => Steps.IndexOf(SelectedStep) > 0;

    partial void OnSelectedStepChanged(StepViewModel? oldValue, StepViewModel newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnStepPropertyChanged;
        }

        newValue.PropertyChanged += OnStepPropertyChanged;
        _ = ActivateAsync(newValue);
        RaiseNavigationState();
    }

    private void OnStepPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StepViewModel.IsValid))
        {
            RaiseNavigationState();
        }
    }

    private void RaiseNavigationState()
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPrevious));
    }

    private async Task ActivateAsync(StepViewModel step)
    {
        try
        {
            if (_state.IsProjectOpen)
            {
                await _state.ReloadAsync();
            }

            step.Refresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task ReloadAndRefreshAsync()
    {
        await _state.ReloadAsync();
        SelectedStep.Refresh();
    }

    private async Task NavigateToAsync(StepViewModel step)
    {
        SelectedStep = step;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Next()
    {
        if (!SelectedStep.IsValid)
        {
            _workflow.Dialogs.ShowWarning("Complétez cette étape avant de continuer.");
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index < Steps.Count - 1)
        {
            SelectedStep = Steps[index + 1];
            await Task.CompletedTask;
        }
    }

    [RelayCommand]
    private async Task Previous()
    {
        var index = Steps.IndexOf(SelectedStep);
        if (index > 0)
        {
            SelectedStep = Steps[index - 1];
            await Task.CompletedTask;
        }
    }

    [RelayCommand]
    private async Task NewProject()
    {
        if (await _workflow.NewAsync())
        {
            await ReloadAndRefreshAsync();
        }
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        if (await _workflow.OpenAsync())
        {
            await ReloadAndRefreshAsync();
        }
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        await _workflow.FlushAsync();
        if (_state.Repository is null)
        {
            return;
        }

        await _state.Repository.SaveSettingsAsync(new Dictionary<string, string> { ["name"] = _state.ProjectName });
        _state.NotifySaved();
    }

    [RelayCommand]
    private async Task SaveProjectAs()
    {
        if (await _workflow.SaveAsAsync(_state.ProjectName + ".edt"))
        {
            await ReloadAndRefreshAsync();
        }
    }

    [RelayCommand]
    private async Task DuplicateProject()
    {
        await _workflow.DuplicateAsync();
    }

    [RelayCommand]
    private static void Exit() => System.Windows.Application.Current.Shutdown();

    private static void AssignIcons(
        ProjectStepViewModel project,
        WeekStepViewModel week,
        ClassesStepViewModel classes,
        SubjectsStepViewModel subjects,
        TeachersStepViewModel teachers,
        ProgramStepViewModel program,
        GenerationStepViewModel generation,
        ResultsStepViewModel results,
        ExportsStepViewModel exports,
        SettingsStepViewModel settings)
    {
        project.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.Home;
        week.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.Calendar;
        classes.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.School;
        subjects.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.BookOpen;
        teachers.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.AccountGroup;
        program.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.ClipboardList;
        generation.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.PlayCircle;
        results.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.Table;
        exports.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.FileExport;
        settings.Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.Cog;
    }
}
