namespace Timetable.Application.Models;

/// <summary>Statistiques affichées dans la vue « Résultats » (FR-11).</summary>
public sealed record ClassStatistics(
    int ClassId,
    string ClassName,
    int AssignedSessions,
    int AvailableSlots,
    int HoleCount)
{
    public double FillRate => AvailableSlots == 0 ? 0 : (double)AssignedSessions / AvailableSlots;
}

/// <summary>Charge hebdomadaire d'un enseignant.</summary>
public sealed record TeacherStatistics(
    int TeacherId,
    string TeacherName,
    string SubjectName,
    int WeeklySessions,
    int HoleCount);

/// <summary>Synthèse globale de l'emploi du temps.</summary>
public sealed record TimetableStatistics(
    IReadOnlyList<ClassStatistics> Classes,
    IReadOnlyList<TeacherStatistics> Teachers)
{
    public static TimetableStatistics Empty { get; } =
        new(Array.Empty<ClassStatistics>(), Array.Empty<TeacherStatistics>());
}
