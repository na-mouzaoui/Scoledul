using FluentAssertions;
using Timetable.Application.Models;
using Timetable.Application.Services;
using Timetable.Testing;

namespace Timetable.Application.Tests;

public class PreCheckServiceTests
{
    private readonly PreCheckService _service = new();

    [Fact]
    public void The_reference_dataset_passes_pre_validation()
    {
        var report = _service.Validate(ReferenceDatasets.BuildReferenceA());

        report.HasBlockingIssues.Should().BeFalse();
    }

    [Fact]
    public void A_week_without_slots_is_blocking()
    {
        var snapshot = Replace(ReferenceDatasets.BuildReferenceA(), slots: Array.Empty<Domain.Entities.TimeSlot>());

        var report = _service.Validate(snapshot);

        report.HasBlockingIssues.Should().BeTrue();
        report.Errors.Should().Contain(e => e.Code == "PRE-01");
    }

    [Fact]
    public void A_subject_without_any_teacher_is_blocking()
    {
        var source = ReferenceDatasets.BuildReferenceA();
        var anglais = source.Subjects.Single(s => s.Name == "Anglais");
        var teachers = source.Teachers.Where(t => t.SubjectId != anglais.Id).ToList();

        var report = _service.Validate(Replace(source, teachers: teachers));

        report.HasBlockingIssues.Should().BeTrue();
        report.Errors.Should().Contain(e => e.Code == "PRE-03" && e.SubjectId == anglais.Id);
    }

    [Fact]
    public void An_empty_program_is_blocking()
    {
        var source = ReferenceDatasets.BuildReferenceA();
        var requirements = source.Requirements
            .Select(r => new Domain.Entities.ClassSubjectRequirement
            {
                ClassId = r.ClassId,
                SubjectId = r.SubjectId,
                WeeklySessions = 0,
            })
            .ToList();

        var report = _service.Validate(Replace(source, requirements: requirements));

        report.HasBlockingIssues.Should().BeTrue();
        report.Errors.Should().Contain(e => e.Code == "PRE-VIDE");
    }

    [Fact]
    public void A_class_without_any_session_is_reported_as_a_warning()
    {
        var source = ReferenceDatasets.BuildReferenceA();
        var requirements = source.Requirements
            .Select(r => r.ClassId == 1
                ? new Domain.Entities.ClassSubjectRequirement
                {
                    ClassId = 1,
                    SubjectId = r.SubjectId,
                    WeeklySessions = 0,
                }
                : r)
            .ToList();

        var report = _service.Validate(Replace(source, requirements: requirements));

        report.HasBlockingIssues.Should().BeFalse();
        report.Warnings.Should().Contain(w => w.Code == "PRE-CLASSE-VIDE" && w.ClassId == 1);
    }

    [Fact] // AC-05
    public void A_class_over_capacity_is_blocked_with_the_class_name()
    {
        var source = ReferenceDatasets.BuildReferenceA();
        var maths = source.Subjects.Single(s => s.Name == "Mathématiques");

        var requirements = source.Requirements
            .Select(r => r.ClassId == 1 && r.SubjectId == maths.Id
                ? new Domain.Entities.ClassSubjectRequirement { ClassId = 1, SubjectId = maths.Id, WeeklySessions = 40 }
                : r)
            .ToList();

        var report = _service.Validate(Replace(source, requirements: requirements));

        report.HasBlockingIssues.Should().BeTrue();
        report.Errors.Should().Contain(e => e.Code == "PRE-06" && e.Message.Contains("Classe 1"));
    }

    [Fact]
    public void A_block_subject_volume_must_be_a_multiple_of_the_block_size()
    {
        var source = ReferenceDatasets.BuildReferenceA();
        var sport = source.Subjects.Single(s => s.Name == "Sport");

        var requirements = source.Requirements
            .Select(r => r.ClassId == 1 && r.SubjectId == sport.Id
                ? new Domain.Entities.ClassSubjectRequirement { ClassId = 1, SubjectId = sport.Id, WeeklySessions = 3 }
                : r)
            .ToList();

        var report = _service.Validate(Replace(source, requirements: requirements));

        report.Errors.Should().Contain(e => e.Code == "PRE-05");
    }

    [Fact]
    public void A_lightly_loaded_week_is_reported_as_information()
    {
        var report = _service.Validate(ReferenceDatasets.BuildReferenceA());
        report.Information.Should().Contain(i => i.Code == "PRE-INFO");
    }

    private static ProjectSnapshot Replace(
        ProjectSnapshot source,
        IReadOnlyList<Domain.Entities.TimeSlot>? slots = null,
        IReadOnlyList<Domain.Entities.Teacher>? teachers = null,
        IReadOnlyList<Domain.Entities.ClassSubjectRequirement>? requirements = null) => new()
        {
            Name = source.Name,
            SlotDurationMinutes = source.SlotDurationMinutes,
            Days = source.Days,
            Slots = slots ?? source.Slots,
            Classes = source.Classes,
            Subjects = source.Subjects,
            Teachers = teachers ?? source.Teachers,
            Requirements = requirements ?? source.Requirements,
            Versions = source.Versions,
            Lessons = source.Lessons,
        };
}
