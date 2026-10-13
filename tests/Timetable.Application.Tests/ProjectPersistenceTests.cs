using FluentAssertions;
using Timetable.Application.Abstractions;
using Timetable.Domain.Entities;
using Timetable.Infrastructure.Services;
using Timetable.Testing;

namespace Timetable.Application.Tests;

/// <summary>AC-08 : fermer puis rouvrir un fichier .edt restitue exactement les mêmes données.</summary>
public class ProjectPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "timetable-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // nettoyage best-effort
        }
    }

    [Fact]
    public async Task A_new_project_is_seeded_with_defaults()
    {
        var (repository, _) = await CreateAsync();
        var snapshot = await repository.LoadAsync();

        snapshot.Days.Should().HaveCount(7);
        snapshot.Days.Count(d => d.IsActive).Should().Be(5);
        snapshot.Subjects.Should().HaveCount(8);
        snapshot.SlotDurationMinutes.Should().Be(60);
        // Les créneaux doivent exister dès la création (5 jours × 8 créneaux de 60 min de 08:00 à 16:00).
        snapshot.Slots.Should().HaveCount(40);
        await repository.DisposeAsync();
    }

    [Fact]
    public async Task A_round_trip_preserves_the_whole_project()
    {
        var (repository, path) = await CreateAsync();

        var source = ReferenceDatasets.BuildReferenceA();
        await repository.SaveWeekAsync(source.Days, source.Slots);

        // Les créneaux sont réinsérés par SaveWeekAsync : on récupère leurs identifiants réels.
        var week = await repository.LoadAsync();
        week.Slots.Should().HaveCount(30);
        var slotIds = week.Slots.Select(s => s.Id).ToList();

        var classIds = new List<int>();
        foreach (var schoolClass in source.Classes)
        {
            var saved = await repository.SaveClassAsync(new SchoolClass
            {
                Name = schoolClass.Name,
                Position = schoolClass.Position,
            });
            classIds.Add(saved.Id);
        }

        var teacherIds = new List<int>();
        foreach (var teacher in source.Teachers)
        {
            var saved = await repository.SaveTeacherAsync(new Teacher
            {
                LastName = teacher.LastName,
                FirstName = teacher.FirstName,
                SubjectId = teacher.SubjectId,
            });
            teacherIds.Add(saved.Id);
        }

        var requirements = source.Requirements
            .Where(r => r.ClassId == 1)
            .Select(r => new ClassSubjectRequirement { SubjectId = r.SubjectId, WeeklySessions = r.WeeklySessions })
            .ToList();
        await repository.SaveRequirementsAsync(classIds[0], requirements);

        await repository.SetTeacherUnavailabilitiesAsync(teacherIds[0], new[] { slotIds[4], slotIds[5] });

        var version = await repository.SaveVersionAsync(new TimetableVersion
        {
            Name = "Version 1",
            IsSelected = true,
            CreatedAtUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
        });

        await repository.SaveLessonsAsync(version.Id, new List<Lesson>
        {
            new() { VersionId = version.Id, ClassId = classIds[0], SubjectId = 1, TeacherId = teacherIds[0], TimeSlotId = slotIds[0] },
            new() { VersionId = version.Id, ClassId = classIds[0], SubjectId = 6, TeacherId = teacherIds[10], TimeSlotId = slotIds[1], BlockKey = "b" },
            new() { VersionId = version.Id, ClassId = classIds[0], SubjectId = 6, TeacherId = teacherIds[10], TimeSlotId = slotIds[2], BlockKey = "b" },
        });

        await repository.DisposeAsync();

        // Réouverture.
        var settings = new TestAppSettingsService(_root);
        var reopened = await new ProjectFileService(settings).OpenAsync(path);
        var snapshot = await reopened.LoadAsync();

        snapshot.Slots.Should().HaveCount(30);
        snapshot.Classes.Should().HaveCount(3);
        snapshot.Classes.Should().Contain(c => c.Name == "Classe 1");
        snapshot.Teachers.Should().HaveCount(12);
        snapshot.Requirements.Should().HaveCount(6);
        snapshot.Teachers.First(t => t.Id == teacherIds[0]).Unavailabilities.Should().HaveCount(2);
        snapshot.Versions.Should().ContainSingle(v => v.Name == "Version 1" && v.IsSelected);
        snapshot.Lessons.Should().HaveCount(3);
        snapshot.SelectedVersion!.Name.Should().Be("Version 1");

        await reopened.DisposeAsync();
    }

    [Fact]
    public async Task Saving_the_week_twice_does_not_duplicate_slots()
    {
        var (repository, _) = await CreateAsync();
        var source = ReferenceDatasets.BuildReferenceA();

        await repository.SaveWeekAsync(source.Days, source.Slots);
        await repository.SaveWeekAsync(source.Days, source.Slots);

        var snapshot = await repository.LoadAsync();
        snapshot.Slots.Should().HaveCount(30);
        await repository.DisposeAsync();
    }

    [Fact]
    public async Task Opening_a_project_creates_a_backup_copy()
    {
        var (repository, path) = await CreateAsync();
        await repository.DisposeAsync();

        var settings = new TestAppSettingsService(_root);
        var service = new ProjectFileService(settings);
        var reopened = await service.OpenAsync(path);
        await reopened.DisposeAsync();

        var backupDirectory = Path.Combine(Path.GetDirectoryName(path)!, ".backups");
        Directory.GetFiles(backupDirectory, "*.edt").Should().NotBeEmpty();
    }

    private async Task<(ITimetableRepository Repository, string Path)> CreateAsync()
    {
        var settings = new TestAppSettingsService(_root);
        var service = new ProjectFileService(settings);
        var path = Path.Combine(settings.Current.DefaultProjectsDirectory, "projet.edt");
        var repository = await service.CreateNewAsync(path);
        return (repository, path);
    }
}
