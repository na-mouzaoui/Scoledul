using System.Globalization;
using System.Resources;

namespace Timetable.Desktop.Resources;

/// <summary>
/// Accès aux chaînes traduites (NFR-13). Les textes sont stockés dans
/// <c>Strings.resx</c> (français, culture neutre) et ses déclinaisons (<c>Strings.en.resx</c>…).
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("Timetable.Desktop.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key)
        => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string AppTitle => Get("App.Title");

    public static string NavProject => Get("Nav.Project");

    public static string NavWeek => Get("Nav.Week");

    public static string NavClasses => Get("Nav.Classes");

    public static string NavSubjects => Get("Nav.Subjects");

    public static string NavTeachers => Get("Nav.Teachers");

    public static string NavProgram => Get("Nav.Program");

    public static string NavGeneration => Get("Nav.Generation");

    public static string NavResults => Get("Nav.Results");

    public static string NavExports => Get("Nav.Exports");

    public static string NavSettings => Get("Nav.Settings");

    public static string CommandNew => Get("Command.New");

    public static string CommandOpen => Get("Command.Open");

    public static string CommandSave => Get("Command.Save");

    public static string CommandSaveAs => Get("Command.SaveAs");

    public static string CommandDuplicate => Get("Command.Duplicate");

    public static string ButtonNext => Get("Button.Next");

    public static string ButtonPrevious => Get("Button.Previous");

    public static string ButtonGenerate => Get("Button.Generate");

    public static string ButtonCancel => Get("Button.Cancel");

    public static string ButtonValidate => Get("Button.Validate");

    public static string ButtonUndo => Get("Button.Undo");

    public static string ButtonRedo => Get("Button.Redo");
}
