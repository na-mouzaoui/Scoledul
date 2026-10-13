using Timetable.Application.Models;
using Timetable.Domain.Entities;

namespace Timetable.Application.Abstractions;

/// <summary>Génère la grille de créneaux d'un projet (FR-04). Implémentation déterministe dans Application.</summary>
public interface ISlotGenerator
{
    SlotGenerationResult Generate(IReadOnlyList<SchoolDay> days, int slotDurationMinutes);
}
