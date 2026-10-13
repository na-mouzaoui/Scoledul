using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Application.Models;

/// <summary>État final renvoyé par le solveur CP-SAT (§8).</summary>
public enum SolverStatus
{
    /// <summary>Solution optimale démontrée.</summary>
    Optimal,

    /// <summary>Meilleure solution trouvée dans le temps imparti (non prouvée optimale).</summary>
    Feasible,

    /// <summary>Problème infaisable (aucune solution n'existe).</summary>
    Infeasible,

    /// <summary>Délai dépassé sans aucune solution.</summary>
    Timeout,

    /// <summary>Génération annulée par l'utilisateur.</summary>
    Cancelled,

    /// <summary>État inconnu.</summary>
    Unknown,
}

/// <summary>Séance produite par le solveur.</summary>
public sealed record SolverLesson(
    int ClassId,
    int SubjectId,
    int TeacherId,
    int SlotId,
    string? BlockKey);

/// <summary>Diagnostic d'infaisabilité ou d'échec (§8.5).</summary>
public sealed record SolverDiagnostic(string Code, string Message);

/// <summary>Options de la génération (FR-10).</summary>
public sealed record GenerationOptions
{
    /// <summary>Durée maximale de recherche en secondes (10 à 600, défaut 60).</summary>
    public int MaxTimeSeconds { get; init; } = 60;

    /// <summary>Graine aléatoire ; 0 = aléatoire (permet de reproduire un résultat).</summary>
    public int RandomSeed { get; init; }

    /// <summary>Nombre de threads du solveur.</summary>
    public int NumWorkers { get; init; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>HC-09 : un enseignant n'enseigne qu'à une seule classe sur la semaine.</summary>
    public bool OneClassPerTeacherWeek { get; init; }

    /// <summary>SC-06 : éviter le sport juste avant ou après une pause.</summary>
    public bool AvoidSportAroundBreak { get; init; } = true;
}

/// <summary>Demande complète adressée au solveur.</summary>
public sealed record SolverRequest
{
    public required IReadOnlyList<TimeSlot> Slots { get; init; }

    public required IReadOnlyList<SchoolClass> Classes { get; init; }

    public required IReadOnlyList<Subject> Subjects { get; init; }

    public required IReadOnlyList<Teacher> Teachers { get; init; }

    public required IReadOnlyList<ClassSubjectRequirement> Requirements { get; init; }

    public IReadOnlySet<(int TeacherId, int SlotId)> Unavailabilities { get; init; }
        = new HashSet<(int, int)>();

    /// <summary>Séances verrouillées à conserver (FR-12).</summary>
    public IReadOnlyList<Lesson> LockedLessons { get; init; } = Array.Empty<Lesson>();

    public GenerationWeights Weights { get; init; } = GenerationWeights.Default;

    public GenerationOptions Options { get; init; } = new();
}

/// <summary>Progression de la génération (pour la barre de progression, FR-10).</summary>
public sealed record SolverProgress(double ElapsedSeconds, string Message);

/// <summary>Résultat complet d'une génération.</summary>
public sealed record SolverResult
{
    public SolverStatus Status { get; init; }

    public IReadOnlyList<SolverLesson> Lessons { get; init; } = Array.Empty<SolverLesson>();

    public double Score { get; init; }

    public double ElapsedSeconds { get; init; }

    /// <summary>Délai (s) au bout duquel la première solution valide a été trouvée, ou <c>null</c>.</summary>
    public double? FirstSolutionSeconds { get; init; }

    public int? Seed { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<SolverDiagnostic> Diagnostics { get; init; } = Array.Empty<SolverDiagnostic>();

    public bool HasSolution => Status is SolverStatus.Optimal or SolverStatus.Feasible;

    public static SolverResult Cancelled(double elapsed) => new()
    {
        Status = SolverStatus.Cancelled,
        ElapsedSeconds = elapsed,
        Message = "Génération annulée par l'utilisateur.",
    };
}
