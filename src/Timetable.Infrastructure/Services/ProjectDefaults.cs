using Timetable.Domain.Common;
using Timetable.Domain.Entities;

namespace Timetable.Infrastructure.Services;

/// <summary>Jeu de données initial créé avec un nouveau projet (FR-06).</summary>
internal static class ProjectDefaults
{
    public static readonly (string Name, string Color, int BlockSize)[] Subjects =
    {
        ("Mathématiques", "#4F81BD", 1),
        ("Français", "#C0504D", 1),
        ("Anglais", "#9BBB59", 1),
        ("Histoire-Géographie", "#8064A2", 1),
        ("Sciences", "#4BACC6", 1),
        ("Sport", "#F79646", 2),
        ("Arts plastiques", "#F756D0", 1),
        ("Musique", "#7F6000", 1),
    };

    /// <summary>Jours par défaut : lundi à vendredi actifs de 08:00 à 16:00.</summary>
    public static IEnumerable<SchoolDay> Days()
    {
        foreach (var weekday in WeekdayExtensions.All)
        {
            var active = weekday is >= Weekday.Monday and <= Weekday.Friday;
            yield return new SchoolDay
            {
                Weekday = weekday,
                IsActive = active,
                StartMinute = 8 * 60,
                EndMinute = 16 * 60,
            };
        }
    }
}
