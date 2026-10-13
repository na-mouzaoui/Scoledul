using Timetable.Domain.Entities;

namespace Timetable.Application.Models;

/// <summary>Formats d'export (FR-13).</summary>
public enum ExportFormat
{
    Pdf,
    Excel,
    Csv,
}

/// <summary>Étendue de l'export (regroupement par classe, par enseignant ou vue d'ensemble).</summary>
public enum ExportScope
{
    ByClass,
    ByTeacher,
    ByDayOverview,
}

/// <summary>Document prêt à être exporté : ensemble cohérent de la grille à représenter.</summary>
public sealed class TimetableDocument
{
    public required string ProjectName { get; init; }

    public required IReadOnlyList<SchoolDay> Days { get; init; }

    public required IReadOnlyList<TimeSlot> Slots { get; init; }

    public required IReadOnlyList<SchoolClass> Classes { get; init; }

    public required IReadOnlyList<Subject> Subjects { get; init; }

    public required IReadOnlyList<Teacher> Teachers { get; init; }

    public required IReadOnlyList<Lesson> Lessons { get; init; }

    public string VersionName { get; init; } = string.Empty;

    public ExportScope Scope { get; init; } = ExportScope.ByClass;
}
