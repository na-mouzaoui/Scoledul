namespace Timetable.Domain.Entities;

/// <summary>
/// Séance planifiée (table <c>lesson</c>) : affectation classe × matière × enseignant × créneau.
/// </summary>
public class Lesson
{
    public int Id { get; set; }

    public int VersionId { get; set; }

    public TimetableVersion? Version { get; set; }

    public int ClassId { get; set; }

    public SchoolClass? Class { get; set; }

    public int SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public int TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public int TimeSlotId { get; set; }

    public TimeSlot? TimeSlot { get; set; }

    /// <summary>Identifiant commun aux séances d'un même bloc (sport). <c>null</c> pour une séance isolée.</summary>
    public string? BlockKey { get; set; }

    /// <summary>Verrouillée (FR-12) : conservée lors d'une régénération.</summary>
    public bool IsLocked { get; set; }
}
