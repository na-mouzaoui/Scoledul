using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Timetable.Application.Models;
using Timetable.Application.Services;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;
using Timetable.Solver;
using Timetable.Testing;

namespace Timetable.Solver.Tests;

public class SolverAcceptanceTests
{
    private static GenerationService CreateService()
        => new(new CpSatTimetableSolver(NullLogger<CpSatTimetableSolver>.Instance));

    private static async Task<(ProjectSnapshot Snapshot, SolverResult Result, IReadOnlyList<Lesson> Lessons)> GenerateAsync(
        ProjectSnapshot? snapshot = null,
        int maxSeconds = 3,
        int seed = 12345,
        int numWorkers = 0)
    {
        snapshot ??= ReferenceDatasets.BuildReferenceA();
        var service = CreateService();
        var options = new GenerationOptions
        {
            MaxTimeSeconds = maxSeconds,
            RandomSeed = seed,
            NumWorkers = numWorkers > 0 ? numWorkers : Environment.ProcessorCount,
        };

        var result = await service.GenerateAsync(snapshot, options);
        var lessons = GenerationService.ToLessons(snapshot.SelectedVersion?.Id ?? 1, result);
        return (snapshot, result, lessons);
    }

    [Fact] // AC-01 : solution trouvée rapidement, zéro violation de contrainte dure.
    public async Task ReferenceSetAIsSolvedWithoutHardViolations()
    {
        var (snapshot, result, lessons) = await GenerateAsync(maxSeconds: 10);

        result.HasSolution.Should().BeTrue($"le solveur doit trouver une solution (statut {result.Status})");
        result.FirstSolutionSeconds.Should().NotBeNull();
        result.FirstSolutionSeconds!.Value.Should().BeLessThan(10, "AC-01 : solution trouvée en moins de 10 s");

        var context = snapshot.ToScheduleContext();
        var conflicts = new ScheduleValidator(context).ValidateAll(lessons);
        conflicts.Where(c => c.IsBlocking).Should().BeEmpty();
    }

    [Fact] // HC-03 : volumes exacts
    public async Task AllRequirementVolumesAreRespected()
    {
        var (snapshot, _, lessons) = await GenerateAsync();

        foreach (var requirement in snapshot.Requirements)
        {
            var count = lessons.Count(l => l.ClassId == requirement.ClassId && l.SubjectId == requirement.SubjectId);
            count.Should().Be(requirement.WeeklySessions);
        }

        lessons.Should().HaveCount(54);
    }

    [Fact] // AC-02 : HC-02
    public async Task NoTeacherIsAssignedToTwoClassesOnTheSameSlot()
    {
        var (_, _, lessons) = await GenerateAsync();

        var clashes = lessons
            .GroupBy(l => (l.TeacherId, l.TimeSlotId))
            .Where(g => g.Count() > 1)
            .ToList();

        clashes.Should().BeEmpty();
    }

    [Fact] // AC-03 / AC-04 : sport en blocs adjacents, sans pause intercalée.
    public async Task SportLessonsFormAdjacentBlocks()
    {
        var (snapshot, _, lessons) = await GenerateAsync();
        var sportId = snapshot.Subjects.Single(s => s.Name == "Sport").Id;
        var slotById = snapshot.Slots.ToDictionary(s => s.Id, s => s);

        foreach (var schoolClass in snapshot.Classes)
        {
            var sportLessons = lessons
                .Where(l => l.ClassId == schoolClass.Id && l.SubjectId == sportId)
                .OrderBy(l => slotById[l.TimeSlotId].SlotIndex)
                .ToList();

            sportLessons.Should().HaveCount(2);
            sportLessons.Select(l => l.TeacherId).Distinct().Should().HaveCount(1);

            var first = slotById[sportLessons[0].TimeSlotId];
            var second = slotById[sportLessons[1].TimeSlotId];
            second.DayId.Should().Be(first.DayId);
            second.SlotIndex.Should().Be(first.SlotIndex + 1);
            second.FollowsBreak.Should().BeFalse();
        }
    }

    [Fact]
    public async Task SeedIsPropagatedAndRunsStayStructurallyEquivalent()
    {
        var (snapshot, first, lessonsA) = await GenerateAsync(seed: 777, maxSeconds: 5);
        var (_, second, lessonsB) = await GenerateAsync(seed: 777, maxSeconds: 5);

        first.HasSolution.Should().BeTrue();
        second.HasSolution.Should().BeTrue();
        first.Seed.Should().Be(777);
        first.Score.Should().BeGreaterThanOrEqualTo(0);

        var countsA = lessonsA
            .GroupBy(l => (l.ClassId, l.SubjectId))
            .ToDictionary(g => g.Key, g => g.Count());
        var countsB = lessonsB
            .GroupBy(l => (l.ClassId, l.SubjectId))
            .ToDictionary(g => g.Key, g => g.Count());

        countsB.Should().Equal(countsA);
        snapshot.Requirements.Should().NotBeEmpty();
    }

    [Fact] // AC-05 : besoin supérieur aux créneaux -> infaisable avec diagnostic.
    public async Task OverCapacityProducesInfeasibilityDiagnostic()
    {
        var snapshot = ReferenceDatasets.BuildReferenceA();
        var maths = snapshot.Subjects.Single(s => s.Name == "Mathématiques");

        var inflated = snapshot.Requirements
            .Select(r => r.ClassId == 1 && r.SubjectId == maths.Id
                ? new ClassSubjectRequirement
                {
                    Id = r.Id,
                    ClassId = r.ClassId,
                    SubjectId = r.SubjectId,
                    WeeklySessions = 40,
                }
                : r)
            .ToList();

        var modified = Clone(snapshot, inflated);

        var result = await CreateService().GenerateAsync(modified, new GenerationOptions { MaxTimeSeconds = 5 });

        result.HasSolution.Should().BeFalse();
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Message.Contains("Classe 1"));
    }

    [Fact] // AC-06 : matière sans enseignant -> infaisable avec message explicite.
    public async Task SubjectWithoutTeacherIsInfeasible()
    {
        var snapshot = ReferenceDatasets.BuildReferenceA();
        var anglais = snapshot.Subjects.Single(s => s.Name == "Anglais");
        var withoutEnglishTeachers = snapshot.Teachers.Where(t => t.SubjectId != anglais.Id).ToList();

        var modified = new ProjectSnapshot
        {
            Name = snapshot.Name,
            SlotDurationMinutes = snapshot.SlotDurationMinutes,
            Days = snapshot.Days,
            Slots = snapshot.Slots,
            Classes = snapshot.Classes,
            Subjects = snapshot.Subjects,
            Teachers = withoutEnglishTeachers,
            Requirements = snapshot.Requirements,
            Versions = snapshot.Versions,
        };

        var result = await CreateService().GenerateAsync(modified, new GenerationOptions { MaxTimeSeconds = 5 });

        result.HasSolution.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Message.Contains("Anglais"));
    }

    private static ProjectSnapshot Clone(
        ProjectSnapshot source,
        IReadOnlyList<ClassSubjectRequirement> requirements) => new()
        {
            Name = source.Name,
            SlotDurationMinutes = source.SlotDurationMinutes,
            Days = source.Days,
            Slots = source.Slots,
            Classes = source.Classes,
            Subjects = source.Subjects,
            Teachers = source.Teachers,
            Requirements = requirements,
            Versions = source.Versions,
            Weights = source.Weights,
        };
}
