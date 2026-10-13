using Microsoft.Extensions.DependencyInjection;
using Timetable.Application.Abstractions;

namespace Timetable.Solver;

/// <summary>Enregistrement du solveur CP-SAT.</summary>
public static class SolverServiceCollectionExtensions
{
    public static IServiceCollection AddTimetableSolver(this IServiceCollection services)
    {
        services.AddSingleton<ITimetableSolver, CpSatTimetableSolver>();
        return services;
    }
}
