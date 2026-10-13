using CommunityToolkit.Mvvm.ComponentModel;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;

namespace Timetable.Desktop.Services;

/// <summary>
/// État partagé de la session : projet courant, contenu chargé et état d'enregistrement (FR-01).
/// Les vues s'abonnent à <see cref="SnapshotChanged"/> pour se rafraîchir.
/// </summary>
public sealed partial class AppState : ObservableObject
{
    private readonly IProjectFileService _projectFiles;

    public AppState(IProjectFileService projectFiles) => _projectFiles = projectFiles;

    public ITimetableRepository? Repository { get; private set; }

    [ObservableProperty]
    private ProjectSnapshot _snapshot = ProjectSnapshot.Empty;

    [ObservableProperty]
    private bool _isProjectOpen;

    [ObservableProperty]
    private string _projectName = "Nouveau projet";

    [ObservableProperty]
    private string _projectPath = string.Empty;

    [ObservableProperty]
    private string _saveStatus = "Aucun projet ouvert";

    [ObservableProperty]
    private bool _isDirty;

    public event EventHandler? SnapshotChanged;

    /// <summary>Crée un projet vide à l'emplacement indiqué.</summary>
    public async Task CreateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Repository = await _projectFiles.CreateNewAsync(filePath, cancellationToken);
        await ReloadAsync(cancellationToken);
        SaveStatus = "Projet créé";
    }

    /// <summary>Ouvre un projet existant.</summary>
    public async Task OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Repository = await _projectFiles.OpenAsync(filePath, cancellationToken);
        await ReloadAsync(cancellationToken);
        SaveStatus = "Projet ouvert";
    }

    /// <summary>Recharge le contenu depuis la base.</summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (Repository is null)
        {
            Snapshot = ProjectSnapshot.Empty;
            IsProjectOpen = false;
            ProjectName = "Nouveau projet";
            ProjectPath = string.Empty;
            IsDirty = false;
            SnapshotChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        Snapshot = await Repository.LoadAsync(cancellationToken);
        IsProjectOpen = true;
        ProjectName = Snapshot.Name;
        ProjectPath = Snapshot.FilePath ?? string.Empty;
        IsDirty = false;
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkDirty() => IsDirty = true;

    public void NotifySaved()
    {
        IsDirty = false;
        SaveStatus = $"Enregistré à {DateTime.Now:HH:mm:ss}";
    }
}
