using Timetable.Application.Models;

namespace Timetable.Application.Abstractions;

/// <summary>
/// Solveur de contraintes (Google OR-Tools CP-SAT, §8). Implémenté dans la couche Timetable.Solver.
/// </summary>
public interface ITimetableSolver
{
    /// <summary>
    /// Recherche un emploi du temps satisfaisant toutes les contraintes dures en minimisant
    /// la somme pondérée des contraintes souples.
    /// </summary>
    Task<SolverResult> SolveAsync(
        SolverRequest request,
        IProgress<SolverProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
