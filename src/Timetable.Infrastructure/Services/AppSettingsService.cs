using System.Text.Json;
using Timetable.Application.Models;

namespace Timetable.Infrastructure.Services;

/// <summary>
/// Lecture / écriture des paramètres de l'application dans <c>%APPDATA%\TimetableApp\app-settings.json</c> (FR-15).
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private const string FolderName = "TimetableApp";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public AppSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = Path.Combine(appData, FolderName);
        Directory.CreateDirectory(root);

        _settingsPath = Path.Combine(root, "app-settings.json");
        LogsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName,
            "logs");

        Current = Load();
        if (string.IsNullOrWhiteSpace(Current.DefaultProjectsDirectory))
        {
            Current.DefaultProjectsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Emplois du temps");
        }
    }

    public AppSettings Current { get; }

    public string LogsDirectory { get; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Current, SerializerOptions));
        }
        catch (IOException)
        {
            // Les paramètres sont secondaires : ne jamais faire échouer l'application pour cela.
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Fichier corrompu : on repart de valeurs par défaut.
        }

        return new AppSettings();
    }
}
