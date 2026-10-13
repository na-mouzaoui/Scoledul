using Timetable.Application.Models;

namespace Timetable.Application.Abstractions;

/// <summary>Création, ouverture et duplication des fichiers projet <c>.edt</c> (FR-01).</summary>
public interface IProjectFileService
{
    /// <summary>Dossier par défaut proposé pour les nouveaux projets.</summary>
    string DefaultProjectsDirectory { get; }

    IReadOnlyList<RecentProject> GetRecentProjects();

    void RememberProject(string filePath, string? name = null);

    /// <summary>Crée un nouveau projet vide à l'emplacement indiqué.</summary>
    Task<ITimetableRepository> CreateNewAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Ouvre (et migre si nécessaire) un projet existant, après sauvegarde de sécurité.</summary>
    Task<ITimetableRepository> OpenAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Duplique un projet existant vers un nouvel emplacement.</summary>
    Task DuplicateAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
}
