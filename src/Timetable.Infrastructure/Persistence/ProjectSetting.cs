namespace Timetable.Infrastructure.Persistence;

/// <summary>Table clé/valeur des paramètres du projet (table <c>project_setting</c>).</summary>
public class ProjectSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>Clés connues de la table <c>project_setting</c>.</summary>
public static class ProjectSettingKeys
{
    public const string SlotDurationMinutes = "slot_duration_minutes";
    public const string OneClassPerTeacherWeek = "one_class_per_teacher_week";
    public const string SchemaVersion = "schema_version";
    public const string Name = "name";
}
