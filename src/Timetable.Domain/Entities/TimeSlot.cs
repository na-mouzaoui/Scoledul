namespace Timetable.Domain.Entities;

using Timetable.Domain.Common;

/// <summary>
/// Créneau horaire généré (table <c>time_slot</c>). Un créneau accueille au plus une séance par classe (HC-01).
/// </summary>
public class TimeSlot
{
    public int Id { get; set; }

    public int DayId { get; set; }

    public SchoolDay? Day { get; set; }

    /// <summary>Index croissant du créneau dans la journée (0-based).</summary>
    public int SlotIndex { get; set; }

    public int StartMinute { get; set; }

    public int EndMinute { get; set; }

    /// <summary>Vrai si une pause précède immédiatement ce créneau (empêche l'adjacence avec le précédent).</summary>
    public bool FollowsBreak { get; set; }

    /// <summary>Jour de la semaine ; dénormalisé depuis <see cref="Day"/> pour simplifier le solveur.</summary>
    public Weekday Weekday => Day?.Weekday ?? default;

    public int DurationMinutes => EndMinute - StartMinute;

    /// <summary>Heure de début au format HH:mm (affichage).</summary>
    public string StartLabel => MinuteOfDay.Format(StartMinute);

    /// <summary>Heure de fin au format HH:mm (affichage).</summary>
    public string EndLabel => MinuteOfDay.Format(EndMinute);
}
