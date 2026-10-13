using System.Diagnostics;
using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Solver;

/// <summary>
/// Solveur d'emplois du temps basé sur Google OR-Tools CP-SAT (§8).
/// Les contraintes dures HC-01 à HC-09 sont modélisées exactement ; les contraintes souples
/// SC-01 à SC-06 sont ajoutées à la fonction objectif avec les poids fournis.
/// </summary>
public sealed class CpSatTimetableSolver : ITimetableSolver
{
    private readonly ILogger<CpSatTimetableSolver> _logger;

    public CpSatTimetableSolver(ILogger<CpSatTimetableSolver> logger) => _logger = logger;

    public Task<SolverResult> SolveAsync(
        SolverRequest request,
        IProgress<SolverProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => Solve(request, progress, cancellationToken), CancellationToken.None);

    private SolverResult Solve(
        SolverRequest request,
        IProgress<SolverProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var model = new CpModel();

        var context = new ModelContext(request, model);

        try
        {
            context.CreateVariables();
            context.AddHardConstraints();
            context.AddObjective();
        }
        catch (ModelInfeasibleException ex)
        {
            return new SolverResult
            {
                Status = SolverStatus.Infeasible,
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                Message = ex.Message,
                Diagnostics = PreCheckDiagnostics(request),
            };
        }

        var solver = new CpSolver();
        var parameters = $"max_time_in_seconds:{Math.Clamp(request.Options.MaxTimeSeconds, 1, 3600)};" +
                         $"num_search_workers:{Math.Max(1, request.Options.NumWorkers)}";
        if (request.Options.RandomSeed != 0)
        {
            parameters += $";random_seed:{request.Options.RandomSeed}";
        }

        solver.StringParameters = parameters;

        var callback = new ProgressCallback(progress, stopwatch, cancellationToken);
        progress?.Report(new SolverProgress(0, "Recherche d'une solution…"));

        var status = solver.Solve(model, callback);
        stopwatch.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            return SolverResult.Cancelled(stopwatch.Elapsed.TotalSeconds);
        }

        switch (status)
        {
            case CpSolverStatus.Optimal:
            case CpSolverStatus.Feasible when callback.SolutionCount > 0:
                return new SolverResult
                {
                    Status = status == CpSolverStatus.Optimal ? SolverStatus.Optimal : SolverStatus.Feasible,
                    Lessons = context.Extract(solver),
                    Score = solver.ObjectiveValue,
                    ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                    FirstSolutionSeconds = callback.FirstSolutionSeconds,
                    Seed = request.Options.RandomSeed,
                    Message = status == CpSolverStatus.Optimal
                        ? "Solution optimale trouvée."
                        : "Meilleure solution trouvée dans le temps imparti.",
                };

            case CpSolverStatus.Infeasible:
                _logger.LogWarning("Modèle CP-SAT infaisable.");
                return new SolverResult
                {
                    Status = SolverStatus.Infeasible,
                    ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                    Message = "Aucune solution ne satisfait toutes les contraintes.",
                    Diagnostics = PreCheckDiagnostics(request),
                };

            default:
                return new SolverResult
                {
                    Status = SolverStatus.Timeout,
                    ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                    Message = "Le délai est dépassé sans qu'une solution complète n'ait été trouvée.",
                    Diagnostics = PreCheckDiagnostics(request),
                };
        }
    }

    private static IReadOnlyList<SolverDiagnostic> PreCheckDiagnostics(SolverRequest request)
    {
        var issues = new List<SolverDiagnostic>();
        var slotCount = request.Slots.Count;
        var slotIndex = SlotLayout.BuildIndex(request.Slots);
        var subjectsById = request.Subjects.ToDictionary(s => s.Id);
        var teachersBySubject = request.Teachers
            .GroupBy(t => t.SubjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var schoolClass in request.Classes)
        {
            var total = request.Requirements
                .Where(r => r.ClassId == schoolClass.Id)
                .Sum(r => r.WeeklySessions);

            if (total > slotCount)
            {
                issues.Add(new SolverDiagnostic(
                    "HC-CAPACITY",
                    $"La classe {schoolClass.Name} demande {total} séances mais il n'y a que {slotCount} créneaux."));
            }
        }

        foreach (var subject in request.Subjects)
        {
            var need = request.Requirements
                .Where(r => r.SubjectId == subject.Id)
                .Sum(r => r.WeeklySessions);

            if (need == 0)
            {
                continue;
            }

            teachersBySubject.TryGetValue(subject.Id, out var teachers);
            var capacity = (teachers?.Count ?? 0) * slotCount;
            if (need > capacity)
            {
                issues.Add(new SolverDiagnostic(
                    "HC-CAPACITY-SUBJECT",
                    $"Matière {subject.Name} : besoin de {need} séances pour {capacity} de capacité " +
                    $"({teachers?.Count ?? 0} enseignant(s) × {slotCount} créneaux)."));
            }

            if (subject.BlockSize <= 1)
            {
                continue;
            }

            var blockStarts = request.Slots.Count(s => SlotLayout.CanStartBlock(s, subject.BlockSize, slotIndex));
            if (blockStarts == 0)
            {
                issues.Add(new SolverDiagnostic(
                    "HC-BLOCK",
                    $"Impossible de placer un bloc de {subject.BlockSize} séances de {subject.Name} : " +
                    "aucun couple de créneaux consécutifs sans pause."));
            }
        }

        if (issues.Count == 0)
        {
            issues.Add(new SolverDiagnostic(
                "HC-UNKNOWN",
                "Le modèle ne peut pas satisfaire toutes les contraintes. Vérifiez les indisponibilités " +
                "des enseignants, les enseignants imposés et les capacités."));
        }

        return issues;
    }

    private sealed class ProgressCallback : CpSolverSolutionCallback
    {
        private readonly IProgress<SolverProgress>? _progress;
        private readonly CancellationToken _cancellationToken;
        private readonly Stopwatch _stopwatch;

        public ProgressCallback(
            IProgress<SolverProgress>? progress,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            _progress = progress;
            _cancellationToken = cancellationToken;
            _stopwatch = stopwatch;
        }

        public int SolutionCount { get; private set; }

        public double? FirstSolutionSeconds { get; private set; }

        public override void OnSolutionCallback()
        {
            SolutionCount++;
            FirstSolutionSeconds ??= _stopwatch.Elapsed.TotalSeconds;
            _progress?.Report(new SolverProgress(
                _stopwatch.Elapsed.TotalSeconds,
                $"Solution {SolutionCount} trouvée (objectif : {ObjectiveValue():0})"));

            if (_cancellationToken.IsCancellationRequested)
            {
                StopSearch();
            }
        }
    }
}
