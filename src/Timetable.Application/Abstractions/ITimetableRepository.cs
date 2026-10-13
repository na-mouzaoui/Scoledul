using Timetable.Application.Models;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Application.Abstractions;

/// <summary>
/// Accès persistant au contenu d'un projet <c>.edt</c>. Toutes les écritures sont transactionnelles (NFR-04).
/// Implémenté par Entity Framework Core dans Timetable.Infrastructure.
/// </summary>
public interface ITimetableRepository : IAsyncDisposable
{
    /// <summary>Chemin du fichier <c>.edt</c> ouvert.</summary>
    string FilePath { get; }

    Task<ProjectSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default);

    /// <summary>Remplace la configuration de la semaine (jours, pauses) et la grille de créneaux.</summary>
    Task SaveWeekAsync(
        IReadOnlyList<SchoolDay> days,
        IReadOnlyList<TimeSlot> slots,
        CancellationToken cancellationToken = default);

    Task<SchoolClass> SaveClassAsync(SchoolClass entity, CancellationToken cancellationToken = default);

    Task DeleteClassAsync(int id, CancellationToken cancellationToken = default);

    Task<Subject> SaveSubjectAsync(Subject entity, CancellationToken cancellationToken = default);

    Task DeleteSubjectAsync(int id, CancellationToken cancellationToken = default);

    Task<Teacher> SaveTeacherAsync(Teacher entity, CancellationToken cancellationToken = default);

    Task DeleteTeacherAsync(int id, CancellationToken cancellationToken = default);

    Task SetTeacherUnavailabilitiesAsync(
        int teacherId,
        IReadOnlyCollection<int> slotIds,
        CancellationToken cancellationToken = default);

    /// <summary>Enregistre le programme d'une classe (FR-08).</summary>
    Task SaveRequirementsAsync(
        int classId,
        IReadOnlyList<ClassSubjectRequirement> requirements,
        CancellationToken cancellationToken = default);

    Task SavePreferencesAsync(GenerationWeights weights, CancellationToken cancellationToken = default);

    Task<TimetableVersion> SaveVersionAsync(TimetableVersion version, CancellationToken cancellationToken = default);

    /// <summary>Remplace l'intégralité des séances d'une version (édition manuelle, FR-12).</summary>
    Task SaveLessonsAsync(
        int versionId,
        IReadOnlyList<Lesson> lessons,
        CancellationToken cancellationToken = default);

    Task SetVersionSelectedAsync(int versionId, CancellationToken cancellationToken = default);

    Task DeleteVersionAsync(int versionId, CancellationToken cancellationToken = default);
}
