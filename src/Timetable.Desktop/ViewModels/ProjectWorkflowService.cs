using System.IO;
using System.Windows.Threading;
using Timetable.Application.Abstractions;
using Timetable.Desktop.Services;
using Timetable.Infrastructure.Persistence;

namespace Timetable.Desktop.ViewModels;

/// <summary>
/// Regroupe les opérations sur les fichiers projet (FR-01) et l'enregistrement automatique
/// après chaque modification validée.
/// </summary>
public sealed class ProjectWorkflowService
{
    public const string ProjectFilter = "Projet d'emploi du temps (*.edt)|*.edt|Tous les fichiers (*.*)|*.*";

    private readonly AppState _state;
    private readonly IDialogService _dialogs;
    private readonly IProjectFileService _files;

    private DispatcherTimer? _autoSaveTimer;
    private Func<CancellationToken, Task>? _pendingSave;

    public ProjectWorkflowService(AppState state, IDialogService dialogs, IProjectFileService files)
    {
        _state = state;
        _dialogs = dialogs;
        _files = files;
    }

    public AppState State => _state;

    public IDialogService Dialogs => _dialogs;

    public event EventHandler? ProjectChanged;

    public async Task<bool> NewAsync()
    {
        var path = _dialogs.SaveFile(ProjectFilter, "Nouveau projet.edt", _files.DefaultProjectsDirectory);
        return path is not null && await OpenAtAsync(path, create: true);
    }

    public async Task<bool> OpenAsync()
    {
        var path = _dialogs.OpenFile(ProjectFilter, _files.DefaultProjectsDirectory);
        return path is not null && await OpenAtAsync(path, create: false);
    }

    public async Task<bool> OpenAtAsync(string path, bool create)
    {
        try
        {
            if (create)
            {
                await _state.CreateAsync(path);
            }
            else
            {
                await _state.OpenAsync(path);
            }

            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Impossible d'ouvrir le projet :\n{ex.Message}");
            return false;
        }
    }

    public async Task SaveNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_state.Repository is null)
        {
            return;
        }

        await _state.Repository.SaveSettingsAsync(
            new Dictionary<string, string> { [ProjectSettingKeys.Name] = name },
            cancellationToken);
        _state.NotifySaved();
    }

    public async Task<bool> SaveAsAsync(string? suggestedName = null)
    {
        if (_state.Repository is null)
        {
            return false;
        }

        var suggested = suggestedName ?? Path.GetFileName(_state.ProjectPath);
        var target = _dialogs.SaveFile(ProjectFilter, suggested, _files.DefaultProjectsDirectory);
        if (target is null)
        {
            return false;
        }

        try
        {
            await _files.DuplicateAsync(_state.ProjectPath, target);
            await _state.OpenAsync(target);
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Impossible d'enregistrer sous :\n{ex.Message}");
            return false;
        }
    }

    public async Task<bool> DuplicateAsync()
    {
        if (_state.Repository is null)
        {
            return false;
        }

        var target = _dialogs.SaveFile(
            ProjectFilter,
            "Copie de " + Path.GetFileName(_state.ProjectPath),
            _files.DefaultProjectsDirectory);

        if (target is null)
        {
            return false;
        }

        try
        {
            await _files.DuplicateAsync(_state.ProjectPath, target);
            _dialogs.ShowInformation("Le projet a été dupliqué.");
            return true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Impossible de dupliquer le projet :\n{ex.Message}");
            return false;
        }
    }

    /// <summary>Programme un enregistrement automatique différé (FR-01).</summary>
    public void QueueSave(Func<CancellationToken, Task> save)
    {
        _pendingSave = save;
        _autoSaveTimer ??= CreateTimer();
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    /// <summary>Force l'enregistrement immédiat des modifications en attente.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _autoSaveTimer?.Stop();
        var pending = _pendingSave;
        _pendingSave = null;
        if (pending is not null)
        {
            await pending(cancellationToken);
        }
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            await FlushAsync();
        };
        return timer;
    }
}
