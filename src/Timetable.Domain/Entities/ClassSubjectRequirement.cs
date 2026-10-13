namespace Timetable.Domain.Entities;

/// <summary>
/// Programme hebdomadaire d'une matière pour une classe (table <c>class_subject_requirement</c>, FR-08).
/// </summary>
public class ClassSubjectRequirement
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public SchoolClass? Class { get; set; }

    public int SubjectId { get; set; }

    public Subject? Subject { get; set; }

    /// <summary>Nombre de séances par semaine (volume horaire).</summary>
    public int WeeklySessions { get; set; }

    /// <summary>Enseignant imposé (HC-08) ; <c>null</c> si le solveur choisit librement.</summary>
    public int? FixedTeacherId { get; set; }

    public Teacher? FixedTeacher { get; set; }
}
