namespace Timetable.Domain.Entities;

/// <summary>Enseignant (table <c>teacher</c>). Chaque enseignant enseigne une seule matière (hypothèse §4.4).</summary>
public class Teacher
{
    public int Id { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public int SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public List<TeacherUnavailability> Unavailabilities { get; set; } = new();

    public string FullName => $"{LastName} {FirstName}".Trim();

    /// <summary>Nom affiché « Prénom Nom ».</summary>
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}
