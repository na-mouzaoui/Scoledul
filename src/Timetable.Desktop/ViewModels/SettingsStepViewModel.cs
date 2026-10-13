using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Application.Models;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Paramètres » : langue, thème, dossier des projets, sauvegardes (FR-15).</summary>
public sealed partial class SettingsStepViewModel : StepViewModel
{
    private readonly IAppSettingsService _settings;
    private readonly ThemeService _themeService;
    private readonly LocalizationService _localization;
    private readonly IDialogService _dialogs;

    private bool _loading;

    public SettingsStepViewModel(
        IAppSettingsService settings,
        ThemeService themeService,
        LocalizationService localization,
        IDialogService dialogs)
        : base(Strings.NavSettings)
    {
        _settings = settings;
        _themeService = themeService;
        _localization = localization;
        _dialogs = dialogs;
        Refresh();
    }

    public IReadOnlyList<string> Languages { get; } = new[] { "fr", "en" };

    public IReadOnlyList<string> Themes { get; } = new[] { "Light", "Dark" };

    [ObservableProperty]
    private string _language = "fr";

    [ObservableProperty]
    private string _theme = "Light";

    [ObservableProperty]
    private string _defaultProjectsDirectory = string.Empty;

    [ObservableProperty]
    private int _autoSaveCopies = 5;

    [ObservableProperty]
    private bool _autoSave = true;

    public string LogsDirectory => _settings.LogsDirectory;

    public override void Refresh()
    {
        _loading = true;
        var current = _settings.Current;
        Language = current.Language;
        Theme = current.Theme;
        DefaultProjectsDirectory = current.DefaultProjectsDirectory;
        AutoSaveCopies = current.AutoSaveCopies;
        AutoSave = current.AutoSave;
        _loading = false;
    }

    partial void OnLanguageChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _localization.Apply(value);
        _settings.Current.Language = value;
        _settings.Save();
    }

    partial void OnThemeChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _themeService.Apply(value);
        _settings.Current.Theme = value;
        _settings.Save();
    }

    partial void OnDefaultProjectsDirectoryChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.DefaultProjectsDirectory = value;
        _settings.Save();
    }

    partial void OnAutoSaveCopiesChanged(int value)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.AutoSaveCopies = Math.Max(1, value);
        _settings.Save();
    }

    partial void OnAutoSaveChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.AutoSave = value;
        _settings.Save();
    }

    [RelayCommand]
    private void ChooseDirectory()
    {
        var folder = _dialogs.PickFolder(DefaultProjectsDirectory);
        if (folder is not null)
        {
            DefaultProjectsDirectory = folder;
        }
    }

    [RelayCommand]
    private void OpenLogsFolder() => OpenFolder(_settings.LogsDirectory);

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Impossible d'ouvrir le dossier :\n{ex.Message}");
        }
    }
}
