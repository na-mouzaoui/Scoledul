using Timetable.Domain.Entities;

namespace Timetable.Application.Models;

/// <summary>
/// Configuration de la semaine réutilisable, mémorisable globalement et applicable à tous les projets
/// (jours actifs, horaires, pauses, durée des créneaux).
/// </summary>
public sealed class WeekConfiguration
{
    public int SlotDurationMinutes { get; set; } = 60;

    public List<DayConfiguration> Days { get; set; } = new();
}

/// <summary>Configuration d'un jour dans une <see cref="WeekConfiguration"/>.</summary>
public sealed class DayConfiguration
{
    public int Weekday { get; set; }

    public bool IsActive { get; set; }

    public int StartMinute { get; set; } = 8 * 60;

    public int EndMinute { get; set; } = 16 * 60;

    public List<BreakConfiguration> Breaks { get; set; } = new();
}

/// <summary>Pause dans une <see cref="WeekConfiguration"/>.</summary>
public sealed class BreakConfiguration
{
    public string Label { get; set; } = "Pause";

    public int StartMinute { get; set; }

    public int EndMinute { get; set; }
}

/// <summary>Conversion entre <see cref="WeekConfiguration"/> et les entités du domaine.</summary>
public static class WeekConfigurationMapper
{
    public static WeekConfiguration From(IReadOnlyList<SchoolDay> days, int slotDurationMinutes) => new()
    {
        SlotDurationMinutes = slotDurationMinutes,
        Days = days.Select(day => new DayConfiguration
        {
            Weekday = (int)day.Weekday,
            IsActive = day.IsActive,
            StartMinute = day.StartMinute,
            EndMinute = day.EndMinute,
            Breaks = day.Breaks.Select(b => new BreakConfiguration
            {
                Label = b.Label,
                StartMinute = b.StartMinute,
                EndMinute = b.EndMinute,
            }).ToList(),
        }).ToList(),
    };

    public static (List<SchoolDay> Days, int SlotDurationMinutes) ToModels(WeekConfiguration configuration)
    {
        var days = new List<SchoolDay>();
        foreach (var day in configuration.Days)
        {
            days.Add(new SchoolDay
            {
                Weekday = (Domain.Common.Weekday)day.Weekday,
                IsActive = day.IsActive,
                StartMinute = day.StartMinute,
                EndMinute = day.EndMinute,
                Breaks = day.Breaks.Select(b => new BreakPeriod
                {
                    Label = b.Label,
                    StartMinute = b.StartMinute,
                    EndMinute = b.EndMinute,
                }).ToList(),
            });
        }

        return (days, configuration.SlotDurationMinutes);
    }
}
