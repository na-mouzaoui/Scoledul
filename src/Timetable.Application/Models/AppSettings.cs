namespace Timetable.Application.Models;

/// <summary>Paramètres persistants de l'application (FR-15), stockés hors des fichiers projet.</summary>
public sealed class AppSettings
{
    public string Language { get; set; } = "fr";

    /// <summary>« Light » ou « Dark ».</summary>
    public string Theme { get; set; } = "Light";

    public string DefaultProjectsDirectory { get; set; } = string.Empty;

    /// <summary>Nombre de sauvegardes automatiques conservées (NFR-04).</summary>
    public int AutoSaveCopies { get; set; } = 5;

    public bool AutoSave { get; set; } = true;

    /// <summary>Projets récemment ouverts (FR-01).</summary>
    public List<RecentProject> RecentProjects { get; set; } = new();

    /// <summary>
    /// Configuration de la semaine appliquée par défaut à tout nouveau projet.
    /// Permet de définir une configuration une fois pour toutes (jours, horaires, pauses, durée).
    /// </summary>
    public WeekConfiguration? GlobalWeek { get; set; }
}

/// <summary>Lecture / écriture des paramètres de l'application.</summary>
public interface IAppSettingsService
{
    AppSettings Current { get; }

    void Save();

    /// <summary>Répertoire où sont écrits les journaux Serilog (NFR-12).</summary>
    string LogsDirectory { get; }
}
