using Timetable.Application.Models;

namespace Timetable.Application.Abstractions;

/// <summary>Export d'un emploi du temps vers un format de fichier (FR-13).</summary>
public interface IExporter
{
    ExportFormat Format { get; }

    /// <summary>Extensions de fichier acceptées (ex. ".pdf").</summary>
    string FileExtension { get; }

    string FilterDescription { get; }

    Task ExportAsync(
        TimetableDocument document,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

/// <summary>Impression directe de la vue courante (FR-13).</summary>
public interface ITimetablePrinter
{
    Task PrintAsync(TimetableDocument document, CancellationToken cancellationToken = default);
}
