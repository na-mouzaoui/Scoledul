using Microsoft.Extensions.DependencyInjection;
using Timetable.Application.Abstractions;
using Timetable.Application.Services;

namespace Timetable.Application;

/// <summary>Enregistrement des services de la couche Application.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTimetableApplication(this IServiceCollection services)
    {
        services.AddSingleton<ISlotGenerator, SlotGenerator>();
        services.AddSingleton<PreCheckService>();
        services.AddSingleton<TimetableStatisticsService>();
        services.AddSingleton<GenerationService>();
        return services;
    }
}
