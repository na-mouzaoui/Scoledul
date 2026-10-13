namespace Timetable.Domain.Entities;

/// <summary>
/// Version (scénario) d'un emploi du temps (table <c>timetable_version</c>, FR-14).
/// </summary>
public class TimetableVersion
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsSelected { get; set; }

    public int? SolverSeed { get; set; }

    public double? Score { get; set; }

    public List<Lesson> Lessons { get; set; } = new();
}
