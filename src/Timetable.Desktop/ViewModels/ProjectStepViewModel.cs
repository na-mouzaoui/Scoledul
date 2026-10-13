using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Accueil / Projet » : nom du projet, projets récents, actions de fichier (FR-01).</summary>
public sealed partial class ProjectStepViewModel : StepViewModel
{
    private readonly ProjectWorkflowService _workflow;
    private readonly IProjectFileService _fileService;
    private readonly DemoDataService _demo;

    public ProjectStepViewModel(
        ProjectWorkflowService workflow,
        IProjectFileService fileService,
        DemoDataService demo)
        : base(Strings.NavProject)
    {
        _workflow = workflow;
        _fileService = fileService;
        _demo = demo;
        ProjectName = workflow.State.ProjectName;
    }

    [ObservableProperty]
    private string _projectName = "Nouveau projet";

    [ObservableProperty]
    private bool _hasRecentProjects;

    public ObservableCollection<RecentProject> RecentProjects { get; } = new();

    public string DefaultDirectory => _fileService.DefaultProjectsDirectory;

    public override void Refresh()
    {
        ProjectName = _workflow.State.ProjectName;
        RecentProjects.Clear();
        foreach (var recent in _fileService.GetRecentProjects())
        {
            RecentProjects.Add(recent);
        }

        HasRecentProjects = RecentProjects.Count > 0;
        IsValid = _workflow.State.IsProjectOpen;
    }

    partial void OnProjectNameChanged(string value)
    {
        if (!_workflow.State.IsProjectOpen)
        {
            return;
        }

        _workflow.State.ProjectName = value;
        _workflow.QueueSave(ct => _workflow.SaveNameAsync(value, ct));
    }

    [RelayCommand]
    private async Task New()
    {
        if (await _workflow.NewAsync())
        {
            Refresh();
        }
    }

    [RelayCommand]
    private async Task Open()
    {
        if (await _workflow.OpenAsync())
        {
            Refresh();
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        await _workflow.SaveNameAsync(ProjectName);
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        if (await _workflow.SaveAsAsync(ProjectName + ".edt"))
        {
            Refresh();
        }
    }

    [RelayCommand]
    private async Task DuplicateProject()
    {
        await _workflow.DuplicateAsync();
    }

    /// <summary>
    /// Charge un jeu d'exemple complet. Si aucun projet n'est ouvert, un projet « Demonstration.edt »
    /// est créé, puis rempli (classes, matières, enseignants et programme).
    /// </summary>
    [RelayCommand]
    private async Task LoadDemo()
    {
        if (!_workflow.State.IsProjectOpen)
        {
            var path = _workflow.Dialogs.SaveFile(
                ProjectWorkflowService.ProjectFilter,
                "Demonstration.edt",
                _fileService.DefaultProjectsDirectory);

            if (path is null)
            {
                return;
            }

            if (!await _workflow.OpenAtAsync(path, create: true))
            {
                return;
            }
        }

        await _demo.LoadIntoCurrentAsync(_workflow.State);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenRecent(RecentProject? project)
    {
        if (project is not null && await _workflow.OpenAtAsync(project.FilePath, create: false))
        {
            Refresh();
        }
    }
}
