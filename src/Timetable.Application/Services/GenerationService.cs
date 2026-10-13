using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Domain.Entities;

namespace Timetable.Application.Services;

/// <summary>
/// Orchestration de la génération : construction de la requête du solveur, exécution et
/// conversion du résultat en séances persistables (FR-10).
/// </summary>
public sealed class GenerationService
{
    private readonly ITimetableSolver _solver;

    public GenerationService(ITimetableSolver solver) => _solver = solver;

    public SolverRequest BuildRequest(ProjectSnapshot snapshot, GenerationOptions options)
    {
        var locked = snapshot.SelectedVersion is { } version
            ? snapshot.LessonsForVersion(version.Id).Where(l => l.IsLocked).ToList()
            : new List<Lesson>();

        return new SolverRequest
        {
            Slots = snapshot.Slots,
            Classes = snapshot.Classes,
            Subjects = snapshot.Subjects,
            Teachers = snapshot.Teachers,
            Requirements = snapshot.Requirements,
            Unavailabilities = snapshot.UnavailabilityPairs,
            LockedLessons = locked,
            Weights = snapshot.Weights,
            Options = options with
            {
                OneClassPerTeacherWeek = options.OneClassPerTeacherWeek || snapshot.OneClassPerTeacherWeek,
            },
        };
    }

    public Task<SolverResult> GenerateAsync(
        ProjectSnapshot snapshot,
        GenerationOptions options,
        IProgress<SolverProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => _solver.SolveAsync(BuildRequest(snapshot, options), progress, cancellationToken);

    /// <summary>Convertit les séances produites par le solveur en entités du domaine.</summary>
    public static IReadOnlyList<Lesson> ToLessons(int versionId, SolverResult result)
        => result.Lessons
            .Select(l => new Lesson
            {
                VersionId = versionId,
                ClassId = l.ClassId,
                SubjectId = l.SubjectId,
                TeacherId = l.TeacherId,
                TimeSlotId = l.SlotId,
                BlockKey = l.BlockKey,
                IsLocked = false,
            })
            .ToList();
}
