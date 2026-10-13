using Microsoft.Extensions.DependencyInjection;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Infrastructure.Export;
using Timetable.Infrastructure.Services;

namespace Timetable.Infrastructure;

/// <summary>Enregistrement des services d'infrastructure (accès données, projets, exports).</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTimetableInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IProjectFileService, ProjectFileService>();
        services.AddSingleton<IExporter, CsvExporter>();
        services.AddSingleton<IExporter, ExcelExporter>();
        services.AddSingleton<IExporter, PdfExporter>();
        return services;
    }
}
