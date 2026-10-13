using FluentAssertions;
using Timetable.Application.Models;
using Timetable.Domain.Entities;
using Timetable.Infrastructure.Export;
using Timetable.Testing;

namespace Timetable.Application.Tests;

/// <summary>Vérifie que les exports PDF, Excel et CSV produisent bien des fichiers exploitables (FR-13).</summary>
public class ExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "timetable-export", Guid.NewGuid().ToString("N"));

    public ExporterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static TimetableDocument BuildDocument(ExportScope scope)
    {
        var snapshot = ReferenceDatasets.BuildReferenceA();
        var version = snapshot.SelectedVersion!;

        var lessons = new List<Lesson>
        {
            new() { Id = 1, VersionId = version.Id, ClassId = 1, SubjectId = 1, TeacherId = 1, TimeSlotId = 1 },
            new() { Id = 2, VersionId = version.Id, ClassId = 1, SubjectId = 2, TeacherId = 3, TimeSlotId = 3 },
            new() { Id = 3, VersionId = version.Id, ClassId = 1, SubjectId = 6, TeacherId = 11, TimeSlotId = 6, BlockKey = "b" },
            new() { Id = 4, VersionId = version.Id, ClassId = 1, SubjectId = 6, TeacherId = 11, TimeSlotId = 7, BlockKey = "b" },
            new() { Id = 5, VersionId = version.Id, ClassId = 2, SubjectId = 1, TeacherId = 2, TimeSlotId = 1 },
        };

        return new TimetableDocument
        {
            ProjectName = "Projet de test",
            VersionName = "Version 1",
            Days = snapshot.Days,
            Slots = snapshot.Slots,
            Classes = snapshot.Classes,
            Subjects = snapshot.Subjects,
            Teachers = snapshot.Teachers,
            Lessons = lessons,
            Scope = scope,
        };
    }

    [Fact]
    public async Task Csv_export_contains_the_lessons()
    {
        var path = Path.Combine(_root, "export.csv");
        await new CsvExporter().ExportAsync(BuildDocument(ExportScope.ByClass), path);

        File.Exists(path).Should().BeTrue();
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("Mathématiques");
        content.Should().Contain("Sport");
        content.Should().Contain("Lundi");
    }

    [Fact]
    public async Task Excel_export_produces_a_workbook()
    {
        var path = Path.Combine(_root, "export.xlsx");
        await new ExcelExporter().ExportAsync(BuildDocument(ExportScope.ByClass), path);

        File.Exists(path).Should().BeTrue();
        new FileInfo(path).Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task Pdf_export_produces_a_document()
    {
        var path = Path.Combine(_root, "export.pdf");
        await new PdfExporter().ExportAsync(BuildDocument(ExportScope.ByClass), path);

        File.Exists(path).Should().BeTrue();
        new FileInfo(path).Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task Teacher_scoped_exports_work()
    {
        var path = Path.Combine(_root, "teacher.pdf");
        await new PdfExporter().ExportAsync(BuildDocument(ExportScope.ByTeacher), path);

        new FileInfo(path).Length.Should().BeGreaterThan(1000);
    }
}
