using System.Windows;
using ControlzEx.Theming;

namespace Timetable.Desktop.Services;

/// <summary>
/// Bascule entre thème clair et sombre (FR-15) en combinant le thème MahApps.Metro
/// (styles des contrôles) et nos dictionnaires de couleurs personnalisées.
/// </summary>
public sealed class ThemeService
{
    private const string LightPath = "Themes/Light.xaml";
    private const string DarkPath = "Themes/Dark.xaml";

    public string CurrentTheme { get; private set; } = "Light";

    public void Apply(string theme)
    {
        CurrentTheme = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";

        // 1) Thème MahApps.Metro (accents, styles des contrôles).
        ThemeManager.Current.ChangeTheme(
            System.Windows.Application.Current,
            CurrentTheme == "Dark" ? "Dark.Blue" : "Light.Blue");

        // 2) Nos couleurs personnalisées utilisé par les cartes, la grille, etc.
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries
            .FirstOrDefault(d => d.Source is not null &&
                                 (d.Source.OriginalString.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                                  d.Source.OriginalString.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase)));

        var replacement = new ResourceDictionary
        {
            Source = new Uri(CurrentTheme == "Dark" ? DarkPath : LightPath, UriKind.Relative),
        };

        if (existing is not null)
        {
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        }
        else
        {
            dictionaries.Add(replacement);
        }
    }
}
