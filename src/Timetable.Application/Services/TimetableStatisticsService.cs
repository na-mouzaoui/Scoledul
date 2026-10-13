using Timetable.Application.Models;

namespace Timetable.Application.Services;

/// <summary>Calcule les statistiques de remplissage et de charge (FR-11).</summary>
public sealed class TimetableStatisticsService
{
    public TimetableStatistics Compute(ProjectSnapshot snapshot, int versionId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var lessons = snapshot.LessonsForVersion(versionId);
        var classes = new List<ClassStatistics>();
        var slotsPerDay = snapshot.Slots
            .GroupBy(s => s.DayId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var schoolClass in snapshot.Classes)
        {
            var classLessons = lessons.Where(l => l.ClassId == schoolClass.Id).ToList();
            var holes = CountHoles(
                classLessons.Select(l => l.TimeSlotId),
                snapshot,
                slotsPerDay);

            classes.Add(new ClassStatistics(
                schoolClass.Id,
                schoolClass.Name,
                classLessons.Count,
                snapshot.Slots.Count,
                holes));
        }

        var teachers = new List<TeacherStatistics>();
        foreach (var teacher in snapshot.Teachers)
        {
            var teacherLessons = lessons.Where(l => l.TeacherId == teacher.Id).ToList();
            var holes = CountHoles(
                teacherLessons.Select(l => l.TimeSlotId),
                snapshot,
                slotsPerDay);

            teachers.Add(new TeacherStatistics(
                teacher.Id,
                teacher.DisplayName,
                snapshot.Subjects.FirstOrDefault(s => s.Id == teacher.SubjectId)?.Name ?? string.Empty,
                teacherLessons.Count,
                holes));
        }

        return new TimetableStatistics(classes, teachers);
    }

    private static int CountHoles(
        IEnumerable<int> slotIds,
        ProjectSnapshot snapshot,
        IReadOnlyDictionary<int, List<Domain.Entities.TimeSlot>> slotsPerDay)
    {
        var byDayId = snapshot.Slots.ToDictionary(s => s.Id, s => s);
        var usedPerDay = new Dictionary<int, List<int>>();

        foreach (var slotId in slotIds.Distinct())
        {
            if (!byDayId.TryGetValue(slotId, out var slot))
            {
                continue;
            }

            if (!usedPerDay.TryGetValue(slot.DayId, out var list))
            {
                list = new List<int>();
                usedPerDay[slot.DayId] = list;
            }

            list.Add(slot.SlotIndex);
        }

        var holes = 0;
        foreach (var (_, indices) in usedPerDay)
        {
            if (indices.Count == 0)
            {
                continue;
            }

            var min = indices.Min();
            var max = indices.Max();
            holes += (max - min + 1) - indices.Count;
        }

        return holes;
    }
}
