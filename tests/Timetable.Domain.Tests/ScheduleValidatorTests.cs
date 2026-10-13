using FluentAssertions;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;
using Timetable.Testing;

namespace Timetable.Domain.Tests;

public class ScheduleValidatorTests
{
    private static readonly ProjectSnapshotFixture Fixture = new();

    private static Lesson Lesson(int classId, int subjectId, int teacherId, int slotId, string? blockKey = null)
        => new()
        {
            Id = classId * 1000 + slotId,
            ClassId = classId,
            SubjectId = subjectId,
            TeacherId = teacherId,
            TimeSlotId = slotId,
            BlockKey = blockKey,
        };

    [Fact]
    public void A_class_cannot_have_two_lessons_on_the_same_slot()
    {
        var context = Fixture.Context;
        var lessons = new[]
        {
            Lesson(1, 1, 1, 1),
            Lesson(1, 2, 3, 1),
        };

        var conflicts = new ScheduleValidator(context).ValidateAll(lessons, checkRequirementCounts: false);

        conflicts.Should().Contain(c => c.Code == "HC-01");
    }

    [Fact]
    public void A_teacher_cannot_teach_two_classes_on_the_same_slot()
    {
        var context = Fixture.Context;
        var lessons = new[]
        {
            Lesson(1, 1, 1, 1),
            Lesson(2, 1, 1, 1),
        };

        var conflicts = new ScheduleValidator(context).ValidateAll(lessons, checkRequirementCounts: false);

        conflicts.Should().Contain(c => c.Code == "HC-02" && c.Message.Contains("déjà en cours"));
    }

    [Fact]
    public void A_teacher_must_teach_their_own_subject()
    {
        var context = Fixture.Context;
        // L'enseignant 1 enseigne les mathématiques (matière 1), pas le français (matière 2).
        var lessons = new[] { Lesson(1, 2, 1, 1) };

        var conflicts = new ScheduleValidator(context).ValidateAll(lessons, checkRequirementCounts: false);

        conflicts.Should().Contain(c => c.Code == "HC-05");
    }

    [Fact]
    public void An_isolated_block_subject_lesson_is_rejected()
    {
        var context = Fixture.Context;
        var sportId = Fixture.SportSubjectId;
        var sportTeacher = Fixture.TeacherOf(sportId);

        var lessons = new[] { Lesson(1, sportId, sportTeacher, 1, blockKey: null) };

        var conflicts = new ScheduleValidator(context).ValidateAll(lessons, checkRequirementCounts: false);

        conflicts.Should().Contain(c => c.Code == "HC-06" && c.Message.Contains("isolée"));
    }

    [Fact]
    public void A_block_split_by_a_break_is_rejected()
    {
        var context = Fixture.Context;
        var sportId = Fixture.SportSubjectId;
        var sportTeacher = Fixture.TeacherOf(sportId);

        // Créneaux 2 et 4 : non consécutifs (le créneau 3 suit la récréation).
        var lessons = new[]
        {
            Lesson(1, sportId, sportTeacher, 3, "blk"),
            Lesson(1, sportId, sportTeacher, 5, "blk"),
        };

        var conflicts = new ScheduleValidator(context).ValidateAll(lessons, checkRequirementCounts: false);

        conflicts.Should().Contain(c => c.Code == "HC-06");
    }

    [Fact]
    public void A_valid_adjacent_block_is_accepted()
    {
        var context = Fixture.Context;
        var sportId = Fixture.SportSubjectId;
        var sportTeacher = Fixture.TeacherOf(sportId);

        // Créneaux 0 et 1 (08:00-09:00 et 09:00-10:00) : adjacents, sans pause.
        var lessons = new[]
        {
            Lesson(1, sportId, sportTeacher, 1, "blk"),
            Lesson(1, sportId, sportTeacher, 2, "blk"),
        };

        var conflicts = new ScheduleValidator(context).ValidateAll(lessons, checkRequirementCounts: false);

        conflicts.Where(c => c.Code == "HC-06").Should().BeEmpty();
    }

    [Fact]
    public void An_unavailable_teacher_slot_is_reported()
    {
        var context = Fixture.Context;
        var lessons = new[] { Lesson(1, 1, 1, 7) }; // créneau déclaré indisponible pour l'enseignant 1

        var conflicts = new ScheduleValidator(context).ValidateAll(lessons, checkRequirementCounts: false);

        conflicts.Should().Contain(c => c.Code == "HC-07");
    }

    [Fact]
    public void Placement_validation_reports_the_conflicting_teacher()
    {
        var context = Fixture.Context;
        var existing = new[] { Lesson(1, 1, 1, 1) };

        var placements = new[] { new PlacementRequest(2, 1, 1, 1) };
        var conflicts = new ScheduleValidator(context).ValidatePlacements(existing, placements);

        conflicts.Should().Contain(c => c.Code == "HC-02");
    }

    /// <summary>Contexte basé sur le jeu de référence A (§12).</summary>
    private sealed class ProjectSnapshotFixture
    {
        public ProjectSnapshotFixture()
        {
            var snapshot = ReferenceDatasets.BuildReferenceA();
            Snapshot = snapshot;
            Context = new ScheduleContext
            {
                Slots = snapshot.Slots,
                Classes = snapshot.Classes,
                Subjects = snapshot.Subjects,
                Teachers = snapshot.Teachers,
                Requirements = snapshot.Requirements,
                Unavailabilities = new HashSet<(int, int)> { (1, 7) },
            };
        }

        public Timetable.Application.Models.ProjectSnapshot Snapshot { get; }

        public ScheduleContext Context { get; }

        public int SportSubjectId => Snapshot.Subjects.Single(s => s.Name == "Sport").Id;

        public int TeacherOf(int subjectId) => Snapshot.Teachers.First(t => t.SubjectId == subjectId).Id;
    }
}
