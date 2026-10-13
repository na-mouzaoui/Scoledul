using System.Globalization;

namespace Timetable.Domain.Common;

/// <summary>
/// Utilitaires de manipulation des heures stockées en minutes depuis minuit (0..1440).
/// </summary>
public static class MinuteOfDay
{
    public const int MinutesPerDay = 1440;

    /// <summary>Formate un nombre de minutes depuis minuit au format 24 h « HH:mm ».</summary>
    public static string Format(int minute)
    {
        var normalized = Math.Clamp(minute, 0, MinutesPerDay);
        var hours = normalized / 60;
        var minutes = normalized % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{minutes:00}");
    }

    /// <summary>Analyse une chaîne « HH:mm » ou « H:mm ». Retourne false si le format est invalide.</summary>
    public static bool TryParse(string? text, out int minute)
    {
        minute = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        if (hours is < 0 or > 24 || minutes is < 0 or > 59)
        {
            return false;
        }

        var value = (hours * 60) + minutes;
        if (value is < 0 or > MinutesPerDay)
        {
            return false;
        }

        minute = value;
        return true;
    }

    /// <summary>Analyse une chaîne « HH:mm » ; lève une exception si le format est invalide.</summary>
    public static int Parse(string text)
        => TryParse(text, out var minute)
            ? minute
            : throw new FormatException($"Heure invalide : « {text} ». Format attendu : HH:mm.");

    /// <summary>Arrondit une heure à un pas de 5 minutes (FR-02 : pas de 5 minutes).</summary>
    public static int RoundToStep(int minute, int step = 5)
    {
        if (step <= 0)
        {
            return minute;
        }

        return (int)Math.Round(minute / (double)step, MidpointRounding.AwayFromZero) * step;
    }
}
