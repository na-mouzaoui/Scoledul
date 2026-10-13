namespace Timetable.Domain.Common;

/// <summary>
/// Jour de la semaine. La valeur numérique suit la convention de la base de données :
/// 1 = lundi … 7 = dimanche (cf. cahier des charges §10).
/// </summary>
public enum Weekday
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    Sunday = 7,
}

/// <summary>Libellés français des jours de la semaine.</summary>
public static class WeekdayExtensions
{
    public static string ToFrenchLabel(this Weekday weekday) => weekday switch
    {
        Weekday.Monday => "Lundi",
        Weekday.Tuesday => "Mardi",
        Weekday.Wednesday => "Mercredi",
        Weekday.Thursday => "Jeudi",
        Weekday.Friday => "Vendredi",
        Weekday.Saturday => "Samedi",
        Weekday.Sunday => "Dimanche",
        _ => weekday.ToString(),
    };

    /// <summary>Libellé abrégé (3 lettres) : « Lun », « Mar », …</summary>
    public static string ToFrenchLabelShort(this Weekday weekday) => weekday switch
    {
        Weekday.Monday => "Lun",
        Weekday.Tuesday => "Mar",
        Weekday.Wednesday => "Mer",
        Weekday.Thursday => "Jeu",
        Weekday.Friday => "Ven",
        Weekday.Saturday => "Sam",
        Weekday.Sunday => "Dim",
        _ => weekday.ToString(),
    };

    /// <summary>Position d'affichage : dimanche = 0, lundi = 1, … samedi = 6 (semaine commençant le dimanche).</summary>
    public static int DisplayIndex(this Weekday weekday) => weekday == Weekday.Sunday ? 0 : (int)weekday;

    /// <summary>Ordre d'affichage des jours de la semaine : dimanche en premier.</summary>
    public static IReadOnlyList<Weekday> DisplayOrder { get; } = new[]
    {
        Weekday.Sunday, Weekday.Monday, Weekday.Tuesday,
        Weekday.Wednesday, Weekday.Thursday, Weekday.Friday, Weekday.Saturday,
    };

    /// <summary>Jours du lundi au vendredi (ordre scolaire usuel).</summary>
    public static IReadOnlyList<Weekday> SchoolWeek { get; } = new[]
    {
        Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday,
        Weekday.Thursday, Weekday.Friday,
    };

    public static IReadOnlyList<Weekday> All { get; } = new[]
    {
        Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday,
        Weekday.Thursday, Weekday.Friday, Weekday.Saturday, Weekday.Sunday,
    };
}
