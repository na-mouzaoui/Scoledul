using System.Text;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Domain.Common;

namespace Timetable.Infrastructure.Export;

/// <summary>Export CSV brut de toutes les séances (FR-13).</summary>
public sealed class CsvExporter : IExporter
{
    public ExportFormat Format => ExportFormat.Csv;

    public string FileExtension => ".csv";

    public string FilterDescription => "Fichier CSV (*.csv)|*.csv";

    public async Task ExportAsync(
        TimetableDocument document,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var slotById = document.Slots.ToDictionary(s => s.Id);
        var subjectById = document.Subjects.ToDictionary(s => s.Id);
        var teacherById = document.Teachers.ToDictionary(t => t.Id);
        var classById = document.Classes.ToDictionary(c => c.Id);

        var builder = new StringBuilder();
        builder.AppendLine("Version;Classe;Matière;Enseignant;Jour;Début;Fin;Verrouillée");

        foreach (var lesson in document.Lessons
                     .OrderBy(l => classById.TryGetValue(l.ClassId, out var c) ? c.Position : 0)
                     .ThenBy(l => slotById.TryGetValue(l.TimeSlotId, out var s) ? (int)s.Weekday : 0)
                     .ThenBy(l => slotById.TryGetValue(l.TimeSlotId, out var s2) ? s2.SlotIndex : 0))
        {
            slotById.TryGetValue(lesson.TimeSlotId, out var slot);
            subjectById.TryGetValue(lesson.SubjectId, out var subject);
            teacherById.TryGetValue(lesson.TeacherId, out var teacher);
            classById.TryGetValue(lesson.ClassId, out var schoolClass);

            var day = slot?.Weekday.ToFrenchLabel() ?? string.Empty;
            var start = slot is null ? string.Empty : MinuteOfDay.Format(slot.StartMinute);
            var end = slot is null ? string.Empty : MinuteOfDay.Format(slot.EndMinute);

            builder.AppendLine(string.Join(';',
                Escape(document.VersionName),
                Escape(schoolClass?.Name),
                Escape(subject?.Name),
                Escape(teacher?.DisplayName),
                Escape(day),
                Escape(start),
                Escape(end),
                lesson.IsLocked ? "1" : "0"));
        }

        await File.WriteAllTextAsync(destinationPath, builder.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }
}
