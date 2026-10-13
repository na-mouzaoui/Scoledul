using System.Globalization;

namespace Timetable.Desktop.Services;

/// <summary>Applique la langue de l'interface via les cultures .NET (NFR-13).</summary>
public sealed class LocalizationService
{
    public string CurrentLanguage { get; private set; } = "fr";

    public void Apply(string language)
    {
        CurrentLanguage = string.IsNullOrWhiteSpace(language) ? "fr" : language;

        try
        {
            var culture = CultureInfo.GetCultureInfo(CurrentLanguage);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            CurrentLanguage = "fr";
        }
    }
}
