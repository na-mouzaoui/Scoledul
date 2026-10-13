using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Desktop.Resources;
using Timetable.Infrastructure.Export;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Exports » : PDF, Excel, CSV et impression (FR-13).</summary>
public sealed partial class ExportsStepViewModel : StepViewModel
{
    private readonly ProjectWorkflowService _workflow;
    private readonly IEnumerable<IExporter> _exporters;
    private readonly ITimetablePrinter _printer;

    public ExportsStepViewModel(
        ProjectWorkflowService workflow,
        IEnumerable<IExporter> exporters,
        ITimetablePrinter printer)
        : base(Strings.NavExports)
    {
        _workflow = workflow;
        _exporters = exporters;
        _printer = printer;
        Formats = exporters.Select(e => e.Format).ToList();
    }

    public IReadOnlyList<ExportFormat> Formats { get; }

    public IReadOnlyList<ExportScope> Scopes { get; } = new[]
    {
        ExportScope.ByClass, ExportScope.ByTeacher, ExportScope.ByDayOverview,
    };

    public ObservableCollection<string> Log { get; } = new();

    [ObservableProperty]
    private ExportFormat _selectedFormat = ExportFormat.Pdf;

    [ObservableProperty]
    private ExportScope _selectedScope = ExportScope.ByClass;

    [ObservableProperty]
    private bool _isBusy;

    public override void Refresh()
    {
        IsValid = _workflow.State.Snapshot.SelectedVersion is not null;
    }

    [RelayCommand]
    private async Task Export()
    {
        var exporter = _exporters.FirstOrDefault(e => e.Format == SelectedFormat);
        if (exporter is null)
        {
            return;
        }

        var document = BuildDocument();
        if (document is null)
        {
            return;
        }

        var suggested = $"{Sanitize(_workflow.State.ProjectName)}-{SelectedScope}.{exporter.FileExtension.TrimStart('.')}";
        var destination = _workflow.Dialogs.SaveFile(exporter.FilterDescription, suggested,
            _workflow.State.Snapshot.FilePath is { Length: > 0 } path
                ? Path.GetDirectoryName(path)
                : null);

        if (destination is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await exporter.ExportAsync(document, destination);
            Log.Add($"[{DateTime.Now:HH:mm:ss}] Export {SelectedFormat} → {destination}");
            _workflow.Dialogs.ShowInformation($"Export réussi :\n{destination}");
        }
        catch (Exception ex)
        {
            Log.Add($"Erreur d'export : {ex.Message}");
            _workflow.Dialogs.ShowError($"L'export a échoué :\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Print()
    {
        var document = BuildDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            await _printer.PrintAsync(document);
        }
        catch (Exception ex)
        {
            _workflow.Dialogs.ShowError($"L'impression a échoué :\n{ex.Message}");
        }
    }

    private TimetableDocument? BuildDocument()
    {
        var snapshot = _workflow.State.Snapshot;
        var version = snapshot.SelectedVersion;
        if (version is null)
        {
            _workflow.Dialogs.ShowWarning("Générez d'abord un emploi du temps.");
            return null;
        }

        return new TimetableDocument
        {
            ProjectName = snapshot.Name,
            VersionName = version.Name,
            Days = snapshot.Days,
            Slots = snapshot.Slots,
            Classes = snapshot.Classes,
            Subjects = snapshot.Subjects,
            Teachers = snapshot.Teachers,
            Lessons = snapshot.LessonsForVersion(version.Id),
            Scope = SelectedScope,
        };
    }

    private static string Sanitize(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }
}
