using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Domain.Entities;

namespace Timetable.Application.Services;

/// <summary>
/// Génération déterministe de la grille de créneaux (français : FR-04). Le temps résiduel trop court
/// pour un créneau complet est ignoré et signalé par un avertissement.
/// </summary>
public sealed class SlotGenerator : ISlotGenerator
{
    public SlotGenerationResult Generate(IReadOnlyList<SchoolDay> days, int slotDurationMinutes)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (slotDurationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotDurationMinutes),
                slotDurationMinutes,
                "La durée d'un créneau doit être strictement positive.");
        }

        var slots = new List<TimeSlot>();
        var warnings = new List<SlotWarning>();

        foreach (var day in days.Where(d => d.IsActive).OrderBy(d => (int)d.Weekday))
        {
            var breaks = day.Breaks.OrderBy(b => b.StartMinute).ToList();
            var cursor = day.StartMinute;
            var slotIndex = 0;
            var followsBreak = false;

            while (cursor + slotDurationMinutes <= day.EndMinute)
            {
                var overlapping = breaks.FirstOrDefault(b => b.Overlaps(cursor, cursor + slotDurationMinutes));
                if (overlapping is not null)
                {
                    cursor = Math.Max(cursor, overlapping.EndMinute);
                    followsBreak = true;
                    continue;
                }

                slots.Add(new TimeSlot
                {
                    DayId = day.Id,
                    Day = day,
                    SlotIndex = slotIndex,
                    StartMinute = cursor,
                    EndMinute = cursor + slotDurationMinutes,
                    FollowsBreak = followsBreak,
                });

                slotIndex++;
                cursor += slotDurationMinutes;
                followsBreak = false;
            }

            var residual = day.EndMinute - cursor;
            if (residual > 0)
            {
                warnings.Add(new SlotWarning(day.Weekday, cursor, residual));
            }
        }

        return new SlotGenerationResult(slots, warnings);
    }
}
