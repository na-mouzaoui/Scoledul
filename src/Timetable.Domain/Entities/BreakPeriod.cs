namespace Timetable.Domain.Entities;

/// <summary>
/// Pause (récréation, déjeuner…) d'un jour donné (table <c>break_period</c>).
/// Une pause interrompt la continuité des créneaux (cf. FR-03 / FR-04).
/// </summary>
public class BreakPeriod
{
    public int Id { get; set; }

    public int DayId { get; set; }

    public SchoolDay? Day { get; set; }

    public string Label { get; set; } = string.Empty;

    public int StartMinute { get; set; }

    public int EndMinute { get; set; }

    public int DurationMinutes => EndMinute - StartMinute;

    public bool Overlaps(int start, int end) => StartMinute < end && start < EndMinute;

    public BreakPeriod Clone() => new()
    {
        Id = Id,
        DayId = DayId,
        Label = Label,
        StartMinute = StartMinute,
        EndMinute = EndMinute,
    };
}
