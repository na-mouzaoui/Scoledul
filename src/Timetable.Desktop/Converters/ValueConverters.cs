using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Timetable.Domain.Common;
using Timetable.Domain.Scheduling;

namespace Timetable.Desktop.Converters;

/// <summary>Convertit true → Visible, false → Collapsed (paramètre « Invert » pour inverser).</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverse un booléen.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>Convertit une couleur hexadécimale en pinceau (FR-11 : séances colorées par matière).</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Brushes.Transparent;
        }

        if (!Cache.TryGetValue(hex, out var brush))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex)!;
                brush = new SolidColorBrush(color);
                brush.Freeze();
            }
            catch (FormatException)
            {
                brush = Brushes.Transparent;
            }

            Cache[hex] = brush;
        }

        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Affiche un nombre de minutes depuis minuit au format HH:mm.</summary>
public sealed class MinuteToTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int minute ? MinuteOfDay.Format(minute) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text && MinuteOfDay.TryParse(text, out var minute) ? minute : Binding.DoNothing;
}

/// <summary>Convertit la gravité d'un conflit en pinceau.</summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ConflictSeverity.Error => System.Windows.Application.Current.TryFindResource("ErrorBrush") ?? Brushes.Red,
            ConflictSeverity.Warning => System.Windows.Application.Current.TryFindResource("WarningBrush") ?? Brushes.Orange,
            _ => System.Windows.Application.Current.TryFindResource("InfoBrush") ?? Brushes.SteelBlue,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convertit un booléen en libellé Oui / Non.</summary>
public sealed class BooleanToYesNoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Oui" : "Non";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
