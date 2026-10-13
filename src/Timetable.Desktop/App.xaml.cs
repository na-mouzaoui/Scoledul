using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Timetable.Application;
using Timetable.Application.Abstractions;
using Timetable.Desktop.Services;
using Timetable.Desktop.ViewModels;
using Timetable.Infrastructure;
using Timetable.Infrastructure.Services;
using Timetable.Solver;

namespace Timetable.Desktop;

/// <summary>Point d'entrée de l'application WPF : configuration de l'injection de dépendances et de Serilog.</summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = new AppSettingsService();
        ConfigureLogging(settings.LogsDirectory);

        _services = BuildServices(settings);

        var localization = _services.GetRequiredService<LocalizationService>();
        localization.Apply(settings.Current.Language);
        var theme = _services.GetRequiredService<ThemeService>();
        theme.Apply(settings.Current.Theme);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Erreur non gérée");
            MessageBox.Show(
                $"Une erreur inattendue s'est produite :\n{args.Exception.Message}\n\n" +
                $"Consultez les journaux dans :\n{settings.LogsDirectory}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        Log.Information("Démarrage de l'application ({Version})", typeof(App).Assembly.GetName().Version);

        var window = new MainWindow
        {
            DataContext = _services.GetRequiredService<ShellViewModel>(),
        };

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Arrêt de l'application");
        Log.CloseAndFlush();
        _services?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureLogging(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logsDirectory, "timetable-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();
    }

    private static ServiceProvider BuildServices(Timetable.Application.Models.IAppSettingsService settings)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(settings);
        services.AddTimetableApplication();
        services.AddTimetableSolver();
        services.AddTimetableInfrastructure();

        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ITimetablePrinter, TimetablePrinter>();
        services.AddSingleton<AppState>();
        services.AddSingleton<ProjectWorkflowService>();
        services.AddSingleton<DemoDataService>();

        services.AddSingleton<ProjectStepViewModel>();
        services.AddSingleton<WeekStepViewModel>();
        services.AddSingleton<ClassesStepViewModel>();
        services.AddSingleton<SubjectsStepViewModel>();
        services.AddSingleton<TeachersStepViewModel>();
        services.AddSingleton<ProgramStepViewModel>();
        services.AddSingleton<GenerationStepViewModel>();
        services.AddSingleton<ResultsStepViewModel>();
        services.AddSingleton<ExportsStepViewModel>();
        services.AddSingleton<SettingsStepViewModel>();
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }
}
