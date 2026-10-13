using Timetable.Domain.Common;
using Timetable.Domain.Entities;

namespace Timetable.Domain.Scheduling;

/// <summary>Demande de placement d'une séance (utilisée pour la validation des déplacements, FR-12).</summary>
public sealed record PlacementRequest(
    int ClassId,
    int SubjectId,
    int TeacherId,
    int SlotId,
    string? BlockKey = null);

/// <summary>
/// Contexte complet nécessaire à la validation des contraintes dures d'un emploi du temps.
/// </summary>
public sealed class ScheduleContext
{
    public required IReadOnlyList<TimeSlot> Slots { get; init; }

    public required IReadOnlyList<SchoolClass> Classes { get; init; }

    public required IReadOnlyList<Subject> Subjects { get; init; }

    public required IReadOnlyList<Teacher> Teachers { get; init; }

    public required IReadOnlyList<ClassSubjectRequirement> Requirements { get; init; }

    public IReadOnlySet<(int TeacherId, int SlotId)> Unavailabilities { get; init; }
        = new HashSet<(int, int)>();

    /// <summary>HC-09 : un enseignant n'enseigne qu'à une seule classe sur la semaine.</summary>
    public bool OneClassPerTeacherWeek { get; init; }

    private Dictionary<int, TimeSlot>? _slotsById;
    private Dictionary<int, Subject>? _subjectsById;
    private Dictionary<int, Teacher>? _teachersById;
    private Dictionary<(int DayId, int SlotIndex), TimeSlot>? _slotIndex;

    public IReadOnlyDictionary<int, TimeSlot> SlotsById => _slotsById ??= Slots.ToDictionary(s => s.Id);

    public IReadOnlyDictionary<int, Subject> SubjectsById => _subjectsById ??= Subjects.ToDictionary(s => s.Id);

    public IReadOnlyDictionary<int, Teacher> TeachersById => _teachersById ??= Teachers.ToDictionary(t => t.Id);

    public IReadOnlyDictionary<(int DayId, int SlotIndex), TimeSlot> SlotIndex
        => _slotIndex ??= SlotLayout.BuildIndex(Slots);

    public string ClassName(int id) => Classes.FirstOrDefault(c => c.Id == id)?.Name ?? $"classe #{id}";

    public string SubjectName(int id) => Subjects.FirstOrDefault(s => s.Id == id)?.Name ?? $"matière #{id}";

    public string TeacherName(int id) => Teachers.FirstOrDefault(t => t.Id == id)?.DisplayName ?? $"enseignant #{id}";

    public string SlotLabel(int id)
        => SlotsById.TryGetValue(id, out var slot)
            ? $"{slot.Weekday.ToFrenchLabel()} {MinuteOfDay.Format(slot.StartMinute)}"
            : $"créneau #{id}";
}
