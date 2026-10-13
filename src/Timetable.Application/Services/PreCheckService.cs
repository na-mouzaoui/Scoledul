using Timetable.Application.Models;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Application.Services;

/// <summary>
/// Vérification préalable à la génération (FR-09). Distingue les problèmes bloquants (erreurs)
/// des avertissements et informations.
/// </summary>
public sealed class PreCheckService
{
    public PreCheckReport Validate(ProjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var issues = new List<PreCheckIssue>();
        var slots = snapshot.Slots;
        var slotCount = slots.Count;
        var slotIndex = SlotLayout.BuildIndex(slots);

        if (slotCount == 0)
        {
            issues.Add(new PreCheckIssue(
                "PRE-01",
                CheckSeverity.Error,
                "La semaine ne contient aucun créneau de cours. Vérifiez les jours actifs, les horaires, " +
                "la durée d'un créneau et les pauses."));
            return new PreCheckReport(issues);
        }

        if (snapshot.Classes.Count == 0)
        {
            issues.Add(new PreCheckIssue(
                "PRE-02",
                CheckSeverity.Error,
                "Aucune classe n'est définie."));
        }

        CheckRequirements(snapshot, issues);
        CheckProgramNotEmpty(snapshot, issues);
        CheckClassCapacity(snapshot, issues, slotCount);
        CheckSubjectCapacity(snapshot, issues, slotCount);
        CheckBlocks(snapshot, issues, slotIndex);

        if (snapshot.Requirements.Sum(r => r.WeeklySessions) > 0)
        {
            CheckFillRate(snapshot, issues, slotCount);
        }

        return new PreCheckReport(issues);
    }

    /// <summary>
    /// Bloque la génération lorsque le programme est vide (aucune séance hebdomadaire définie),
    /// sinon le solveur produirait un emploi du temps vide sans aucune matière.
    /// </summary>
    private static void CheckProgramNotEmpty(ProjectSnapshot snapshot, List<PreCheckIssue> issues)
    {
        var total = snapshot.Requirements.Sum(r => r.WeeklySessions);
        if (total == 0)
        {
            issues.Add(new PreCheckIssue(
                "PRE-VIDE",
                CheckSeverity.Error,
                "Le programme est vide : aucune séance hebdomadaire n'est définie. " +
                "Renseignez le nombre de séances par matière dans l'étape « Programme », puis relancez la génération."));
            return;
        }

        foreach (var schoolClass in snapshot.Classes)
        {
            var classTotal = snapshot.Requirements
                .Where(r => r.ClassId == schoolClass.Id)
                .Sum(r => r.WeeklySessions);

            if (classTotal == 0)
            {
                issues.Add(new PreCheckIssue(
                    "PRE-CLASSE-VIDE",
                    CheckSeverity.Warning,
                    $"La classe {schoolClass.Name} n'a aucune séance définie : elle restera vide.",
                    ClassId: schoolClass.Id));
            }
        }
    }

    private static void CheckRequirements(ProjectSnapshot snapshot, List<PreCheckIssue> issues)
    {
        foreach (var requirement in snapshot.Requirements)
        {
            var subject = snapshot.Subjects.FirstOrDefault(s => s.Id == requirement.SubjectId);
            if (subject is null)
            {
                continue;
            }

            var teachers = snapshot.Teachers.Where(t => t.SubjectId == requirement.SubjectId).ToList();

            if (requirement.WeeklySessions > 0 && teachers.Count == 0)
            {
                issues.Add(new PreCheckIssue(
                    "PRE-03",
                    CheckSeverity.Error,
                    $"La matière {subject.Name} doit être enseignée à la classe " +
                    $"{snapshot.Classes.FirstOrDefault(c => c.Id == requirement.ClassId)?.Name} " +
                    "mais aucun enseignant de cette matière n'est enregistré.",
                    ClassId: requirement.ClassId,
                    SubjectId: requirement.SubjectId));
            }

            if (requirement.FixedTeacherId is { } fixedId)
            {
                var fixedTeacher = snapshot.Teachers.FirstOrDefault(t => t.Id == fixedId);
                if (fixedTeacher is null)
                {
                    issues.Add(new PreCheckIssue(
                        "PRE-04",
                        CheckSeverity.Error,
                        $"L'enseignant imposé pour {subject.Name} (classe " +
                        $"{snapshot.Classes.FirstOrDefault(c => c.Id == requirement.ClassId)?.Name}) n'existe plus.",
                        ClassId: requirement.ClassId,
                        SubjectId: requirement.SubjectId));
                }
                else if (fixedTeacher.SubjectId != requirement.SubjectId)
                {
                    issues.Add(new PreCheckIssue(
                        "PRE-04",
                        CheckSeverity.Error,
                        $"L'enseignant imposé {fixedTeacher.DisplayName} n'enseigne pas la matière {subject.Name}.",
                        ClassId: requirement.ClassId,
                        SubjectId: requirement.SubjectId,
                        TeacherId: fixedTeacher.Id));
                }
            }

            if (subject.BlockSize > 1 && requirement.WeeklySessions > 0 &&
                requirement.WeeklySessions % subject.BlockSize != 0)
            {
                issues.Add(new PreCheckIssue(
                    "PRE-05",
                    CheckSeverity.Error,
                    $"Le nombre de séances de {subject.Name} pour la classe " +
                    $"{snapshot.Classes.FirstOrDefault(c => c.Id == requirement.ClassId)?.Name} " +
                    $"({requirement.WeeklySessions}) doit être un multiple de {subject.BlockSize}.",
                    ClassId: requirement.ClassId,
                    SubjectId: requirement.SubjectId));
            }
        }
    }

    private static void CheckClassCapacity(ProjectSnapshot snapshot, List<PreCheckIssue> issues, int slotCount)
    {
        foreach (var schoolClass in snapshot.Classes)
        {
            var total = snapshot.Requirements
                .Where(r => r.ClassId == schoolClass.Id)
                .Sum(r => r.WeeklySessions);

            if (total > slotCount)
            {
                issues.Add(new PreCheckIssue(
                    "PRE-06",
                    CheckSeverity.Error,
                    $"La classe {schoolClass.Name} demande {total} séances mais il n'y a que " +
                    $"{slotCount} créneaux disponibles dans la semaine.",
                    ClassId: schoolClass.Id));
            }
        }
    }

    private static void CheckSubjectCapacity(ProjectSnapshot snapshot, List<PreCheckIssue> issues, int slotCount)
    {
        foreach (var subject in snapshot.Subjects)
        {
            var need = snapshot.Requirements
                .Where(r => r.SubjectId == subject.Id)
                .Sum(r => r.WeeklySessions);

            if (need == 0)
            {
                continue;
            }

            var subjectTeachers = snapshot.Teachers.Where(t => t.SubjectId == subject.Id).ToList();
            if (subjectTeachers.Count == 0)
            {
                continue; // déjà signalé par PRE-03
            }

            var capacity = subjectTeachers.Sum(t => Math.Max(0, slotCount - t.Unavailabilities.Count));
            if (need > capacity)
            {
                issues.Add(new PreCheckIssue(
                    "PRE-07",
                    CheckSeverity.Error,
                    $"Matière {subject.Name} : besoin de {need} séances, mais la capacité des " +
                    $"{subjectTeachers.Count} enseignant(s) (en tenant compte de leurs indisponibilités) " +
                    $"n'est que de {capacity} séances.",
                    SubjectId: subject.Id));
            }
        }
    }

    private static void CheckBlocks(
        ProjectSnapshot snapshot,
        List<PreCheckIssue> issues,
        IReadOnlyDictionary<(int DayId, int SlotIndex), TimeSlot> slotIndex)
    {
        var blockSubjects = snapshot.Subjects.Where(s => s.BlockSize > 1).ToList();
        foreach (var subject in blockSubjects)
        {
            var requirements = snapshot.Requirements
                .Where(r => r.SubjectId == subject.Id && r.WeeklySessions > 0)
                .ToList();

            if (requirements.Count == 0)
            {
                continue;
            }

            var startsAvailable = snapshot.Slots
                .Where(s => SlotLayout.CanStartBlock(s, subject.BlockSize, slotIndex))
                .ToList();

            if (startsAvailable.Count == 0)
            {
                issues.Add(new PreCheckIssue(
                    "PRE-08",
                    CheckSeverity.Error,
                    $"Impossible de placer un bloc de {subject.BlockSize} séances de {subject.Name} : " +
                    "aucun couple de créneaux consécutifs sans pause n'est disponible.",
                    SubjectId: subject.Id));
                continue;
            }

            foreach (var requirement in requirements)
            {
                var blocksNeeded = requirement.WeeklySessions / subject.BlockSize;
                if (startsAvailable.Count < blocksNeeded)
                {
                    issues.Add(new PreCheckIssue(
                        "PRE-08",
                        CheckSeverity.Error,
                        $"La classe {snapshot.Classes.FirstOrDefault(c => c.Id == requirement.ClassId)?.Name} " +
                        $"demande {blocksNeeded} bloc(s) de {subject.Name}, mais seulement " +
                        $"{startsAvailable.Count} position(s) de bloc sont possibles dans la semaine.",
                        ClassId: requirement.ClassId,
                        SubjectId: subject.Id));
                }
            }
        }
    }

    private static void CheckFillRate(ProjectSnapshot snapshot, List<PreCheckIssue> issues, int slotCount)
    {
        var total = snapshot.Requirements.Sum(r => r.WeeklySessions);
        var slotsPerClass = slotCount * Math.Max(1, snapshot.Classes.Count);
        if (total < slotsPerClass)
        {
            issues.Add(new PreCheckIssue(
                "PRE-INFO",
                CheckSeverity.Information,
                $"Le programme total ({total} séances) est inférieur à la capacité " +
                $"({slotsPerClass} créneaux disponibles) : des créneaux resteront vides."));
        }
    }
}
