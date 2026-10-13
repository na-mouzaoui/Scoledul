using Timetable.Domain.Entities;

namespace Timetable.Domain.Scheduling;

/// <summary>
/// Utilitaires d'adjacence entre créneaux (FR-04). Deux créneaux sont adjacents si leurs
/// <c>slot_index</c> se suivent dans le même jour et si le second ne suit pas une pause.
/// </summary>
public static class SlotLayout
{
    public static Dictionary<(int DayId, int SlotIndex), TimeSlot> BuildIndex(IEnumerable<TimeSlot> slots)
    {
        var index = new Dictionary<(int DayId, int SlotIndex), TimeSlot>();
        foreach (var slot in slots)
        {
            index[(slot.DayId, slot.SlotIndex)] = slot;
        }

        return index;
    }

    public static bool AreAdjacent(TimeSlot previous, TimeSlot candidate)
        => previous.DayId == candidate.DayId
           && candidate.SlotIndex == previous.SlotIndex + 1
           && !candidate.FollowsBreak;

    /// <summary>Retourne le créneau immédiatement adjacent suivant, ou <c>null</c>.</summary>
    public static TimeSlot? NextAdjacent(
        TimeSlot slot,
        IReadOnlyDictionary<(int DayId, int SlotIndex), TimeSlot> index)
        => index.TryGetValue((slot.DayId, slot.SlotIndex + 1), out var next) && !next.FollowsBreak
            ? next
            : null;

    /// <summary>Vérifie qu'un bloc de <paramref name="blockSize"/> créneaux peut démarrer en <paramref name="start"/>.</summary>
    public static bool CanStartBlock(
        TimeSlot start,
        int blockSize,
        IReadOnlyDictionary<(int DayId, int SlotIndex), TimeSlot> index)
    {
        if (blockSize < 1)
        {
            return false;
        }

        var current = start;
        for (var i = 1; i < blockSize; i++)
        {
            var next = NextAdjacent(current, index);
            if (next is null)
            {
                return false;
            }

            current = next;
        }

        return true;
    }

    /// <summary>Retourne la liste des créneaux d'un bloc démarrant en <paramref name="start"/>.</summary>
    public static IReadOnlyList<TimeSlot> BlockSlots(
        TimeSlot start,
        int blockSize,
        IReadOnlyDictionary<(int DayId, int SlotIndex), TimeSlot> index)
    {
        var result = new List<TimeSlot> { start };
        var current = start;
        for (var i = 1; i < blockSize; i++)
        {
            var next = NextAdjacent(current, index);
            if (next is null)
            {
                break;
            }

            result.Add(next);
            current = next;
        }

        return result;
    }
}
