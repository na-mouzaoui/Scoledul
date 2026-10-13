namespace Timetable.Application.Models;

/// <summary>Gravité d'un point de contrôle de pré-validation (FR-09).</summary>
public enum CheckSeverity
{
    /// <summary>Information : n'empêche pas la génération.</summary>
    Information,

    /// <summary>Avertissement.</summary>
    Warning,

    /// <summary>Bloquant : empêche la génération.</summary>
    Error,
}

/// <summary>Un point de contrôle de pré-validation.</summary>
public sealed record PreCheckIssue(
    string Code,
    CheckSeverity Severity,
    string Message,
    int? ClassId = null,
    int? SubjectId = null,
    int? TeacherId = null);

/// <summary>Rapport de pré-validation (FR-09).</summary>
public sealed record PreCheckReport(IReadOnlyList<PreCheckIssue> Issues)
{
    public static PreCheckReport Empty { get; } = new(Array.Empty<PreCheckIssue>());

    public bool HasBlockingIssues => Issues.Any(i => i.Severity == CheckSeverity.Error);

    public IEnumerable<PreCheckIssue> Errors
        => Issues.Where(i => i.Severity == CheckSeverity.Error);

    public IEnumerable<PreCheckIssue> Warnings
        => Issues.Where(i => i.Severity == CheckSeverity.Warning);

    public IEnumerable<PreCheckIssue> Information
        => Issues.Where(i => i.Severity == CheckSeverity.Information);
}
