using Timetable.Application.Models;

namespace Timetable.Testing;

/// <summary>Implémentation de test des paramètres de l'application, isolée dans un dossier temporaire.</summary>
public sealed class TestAppSettingsService : IAppSettingsService
{
    public TestAppSettingsService(string root)
    {
        Root = root;
        LogsDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(LogsDirectory);

        Current = new AppSettings
        {
            DefaultProjectsDirectory = Path.Combine(root, "projects"),
            AutoSaveCopies = 3,
        };
        Directory.CreateDirectory(Current.DefaultProjectsDirectory);
    }

    public string Root { get; }

    public AppSettings Current { get; }

    public string LogsDirectory { get; }

    public void Save()
    {
    }
}
