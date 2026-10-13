namespace Timetable.Domain.Scheduling;

/// <summary>Gravité d'un conflit affiché dans le panneau « Conflits » (FR-12).</summary>
public enum ConflictSeverity
{
    /// <summary>Information (aucune action requise).</summary>
    Information = 0,

    /// <summary>Avertissement (à vérifier).</summary>
    Warning = 1,

    /// <summary>Erreur : viole une contrainte dure ; bloque la validation.</summary>
    Error = 2,
}

/// <summary>Violation détectée d'une contrainte dure (ou avertissement) sur l'emploi du temps.</summary>
public sealed record ScheduleConflict(
    string Code,
    string Message,
    ConflictSeverity Severity = ConflictSeverity.Error,
    int? ClassId = null,
    int? TeacherId = null,
    int? SubjectId = null,
    int? SlotId = null)
{
    public bool IsBlocking => Severity == ConflictSeverity.Error;
}
