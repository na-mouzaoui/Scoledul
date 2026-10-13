using Timetable.Domain.Common;
using Timetable.Domain.Entities;

namespace Timetable.Application.Models;

/// <summary>Avertissement émis lors de la génération de la grille (temps résiduel trop court, FR-04).</summary>
public sealed record SlotWarning(Weekday Weekday, int StartMinute, int ResidualMinutes);

/// <summary>Résultat de la génération de la grille de créneaux (FR-04).</summary>
public sealed record SlotGenerationResult(
    IReadOnlyList<TimeSlot> Slots,
    IReadOnlyList<SlotWarning> Warnings)
{
    public static SlotGenerationResult Empty { get; } =
        new(Array.Empty<TimeSlot>(), Array.Empty<SlotWarning>());
}
