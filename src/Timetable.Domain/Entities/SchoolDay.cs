namespace Timetable.Domain.Entities;

using Timetable.Domain.Common;

/// <summary>
/// Jour ouvré du projet (table <c>school_day</c>). Porte les horaires d'ouverture et la liste des pauses.
/// </summary>
public class SchoolDay
{
    public int Id { get; set; }

    public Weekday Weekday { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Heure de début du jour, en minutes depuis minuit.</summary>
    public int StartMinute { get; set; }

    /// <summary>Heure de fin du jour, en minutes depuis minuit (<= 1440).</summary>
    public int EndMinute { get; set; }

    public List<BreakPeriod> Breaks { get; set; } = new();

    public List<TimeSlot> Slots { get; set; } = new();

    public int DurationMinutes => EndMinute - StartMinute;

    public SchoolDay Clone() => new()
    {
        Id = Id,
        Weekday = Weekday,
        IsActive = IsActive,
        StartMinute = StartMinute,
        EndMinute = EndMinute,
        Breaks = Breaks.Select(b => b.Clone()).ToList(),
    };
}
