using Timetable.Application.Models;
using Timetable.Application.Services;
using Timetable.Domain.Common;
using Timetable.Domain.Entities;

namespace Timetable.Testing;

/// <summary>
/// Jeux de données de référence du cahier des charges (§12).
/// </summary>
public static class ReferenceDatasets
{
    /// <summary>
    /// Jeu de référence A (petit) : lundi–vendredi 08:00–16:00, créneaux de 60 min,
    /// pause 10:00–10:15 et déjeuner 12:00–13:00 tous les jours ; 3 classes ;
    /// 6 matières ; 2 enseignants par matière ; sport 2 séances/semaine (bloc de 2).
    /// </summary>
    public static ProjectSnapshot BuildReferenceA()
    {
        var subjects = new List<Subject>
        {
            new() { Id = 1, Name = "Mathématiques", ColorHex = "#4F81BD", BlockSize = 1 },
            new() { Id = 2, Name = "Français", ColorHex = "#C0504D", BlockSize = 1 },
            new() { Id = 3, Name = "Anglais", ColorHex = "#9BBB59", BlockSize = 1 },
            new() { Id = 4, Name = "Histoire-Géographie", ColorHex = "#8064A2", BlockSize = 1 },
            new() { Id = 5, Name = "Sciences", ColorHex = "#4BACC6", BlockSize = 1 },
            new() { Id = 6, Name = "Sport", ColorHex = "#F79646", BlockSize = 2 },
        };

        var weekly = new Dictionary<int, int>
        {
            [1] = 4,
            [2] = 4,
            [3] = 3,
            [4] = 3,
            [5] = 2,
            [6] = 2,
        };

        var days = WeekdayExtensions.SchoolWeek
            .Select((weekday, index) => new SchoolDay
            {
                Id = index + 1,
                Weekday = weekday,
                IsActive = true,
                StartMinute = 8 * 60,
                EndMinute = 16 * 60,
                Breaks = new List<BreakPeriod>
                {
                    new() { DayId = index + 1, Label = "Récréation", StartMinute = 10 * 60, EndMinute = (10 * 60) + 15 },
                    new() { DayId = index + 1, Label = "Déjeuner", StartMinute = 12 * 60, EndMinute = 13 * 60 },
                },
            })
            .ToList();

        var slotResult = new SlotGenerator().Generate(days, 60);
        var slots = slotResult.Slots
            .Select((slot, index) =>
            {
                slot.Id = index + 1;
                return slot;
            })
            .ToList();

        var classes = Enumerable.Range(1, 3)
            .Select(i => new SchoolClass { Id = i, Name = $"Classe {i}", Position = i - 1 })
            .ToList();

        var teachers = new List<Teacher>();
        var teacherId = 1;
        foreach (var subject in subjects)
        {
            for (var i = 0; i < 2; i++)
            {
                teachers.Add(new Teacher
                {
                    Id = teacherId,
                    LastName = $"Prof{i + 1}_{subject.Id}",
                    FirstName = subject.Name.Length > 0 ? subject.Name[..1] : "X",
                    SubjectId = subject.Id,
                });
                teacherId++;
            }
        }

        var requirements = new List<ClassSubjectRequirement>();
        var requirementId = 1;
        foreach (var schoolClass in classes)
        {
            foreach (var subject in subjects)
            {
                requirements.Add(new ClassSubjectRequirement
                {
                    Id = requirementId++,
                    ClassId = schoolClass.Id,
                    SubjectId = subject.Id,
                    WeeklySessions = weekly[subject.Id],
                });
            }
        }

        return new ProjectSnapshot
        {
            Name = "Jeu de référence A",
            SlotDurationMinutes = 60,
            Days = days,
            Slots = slots,
            Classes = classes,
            Subjects = subjects,
            Teachers = teachers,
            Requirements = requirements,
            Versions = new List<TimetableVersion>
            {
                new() { Id = 1, Name = "Version 1", IsSelected = true },
            },
        };
    }
}
