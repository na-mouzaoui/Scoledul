namespace Timetable.Domain.Entities;

/// <summary>Indisponibilité déclarée d'un enseignant sur un créneau (table <c>teacher_unavailability</c>, HC-07).</summary>
public class TeacherUnavailability
{
    public int TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public int TimeSlotId { get; set; }

    public TimeSlot? TimeSlot { get; set; }
}
