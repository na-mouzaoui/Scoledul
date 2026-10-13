using Timetable.Application.Models;
using Timetable.Domain.Common;

namespace Timetable.Infrastructure.Export;

/// <summary>Cellule d'une grille d'emploi du temps.</summary>
public sealed record GridCell(string? Text, string? ColorHex, bool IsBreak = false, bool IsUnavailable = false);

/// <summary>Ligne d'une grille : un créneau ou une pause, désigné par sa plage horaire.</summary>
public sealed record GridRow(int StartMinute, int EndMinute, bool IsBreak, string Label)
{
    public string TimeLabel => $"{MinuteOfDay.Format(StartMinute)}–{MinuteOfDay.Format(EndMinute)}";
}

/// <summary>Grille prête à être affichée, exportée ou imprimée.</summary>
public sealed record TimetableGrid(
    string Title,
    IReadOnlyList<string> ColumnHeaders,
    IReadOnlyList<GridRow> Rows,
    IReadOnlyList<IReadOnlyList<GridCell>> Cells);

/// <summary>
/// Construit les grilles « par classe », « par enseignant » ou « vue d'ensemble par jour » (FR-11 / FR-13).
/// </summary>
public static class TimetableGridBuilder
{
    public static IReadOnlyList<TimetableGrid> Build(TimetableDocument document)
        => document.Scope switch
        {
            ExportScope.ByTeacher => BuildByTeacher(document),
            ExportScope.ByDayOverview => BuildByDayOverview(document),
            _ => BuildByClass(document),
        };

    private static IReadOnlyList<TimetableGrid> BuildByClass(TimetableDocument document)
    {
        var grids = new List<TimetableGrid>();
        var rows = BuildRows(document);
        var columns = document.Days.Where(d => d.IsActive).OrderBy(d => d.Weekday.DisplayIndex()).ToList();
        var slotById = document.Slots.ToDictionary(s => s.Id);

        foreach (var schoolClass in document.Classes.OrderBy(c => c.Position))
        {
            var cells = new List<IReadOnlyList<GridCell>>();
            foreach (var row in rows)
            {
                var line = new List<GridCell>();
                foreach (var day in columns)
                {
                    line.Add(ResolveCell(
                        document,
                        row,
                        day,
                        slot => LessonOf(document, schoolClass.Id, slot, byTeacher: false)));
                }

                cells.Add(line);
            }

            grids.Add(new TimetableGrid(
                $"Classe {schoolClass.Name}",
                columns.Select(d => d.Weekday.ToFrenchLabel()).ToList(),
                rows,
                cells));
        }

        _ = slotById;
        return grids;
    }

    private static IReadOnlyList<TimetableGrid> BuildByTeacher(TimetableDocument document)
    {
        var grids = new List<TimetableGrid>();
        var rows = BuildRows(document);
        var columns = document.Days.Where(d => d.IsActive).OrderBy(d => d.Weekday.DisplayIndex()).ToList();

        foreach (var teacher in document.Teachers.OrderBy(t => t.LastName).ThenBy(t => t.FirstName))
        {
            var cells = new List<IReadOnlyList<GridCell>>();
            foreach (var row in rows)
            {
                var line = new List<GridCell>();
                foreach (var day in columns)
                {
                    line.Add(ResolveCell(
                        document,
                        row,
                        day,
                        slot => LessonOf(document, teacher.Id, slot, byTeacher: true)));
                }

                cells.Add(line);
            }

            grids.Add(new TimetableGrid(
                $"Enseignant {teacher.DisplayName}",
                columns.Select(d => d.Weekday.ToFrenchLabel()).ToList(),
                rows,
                cells));
        }

        return grids;
    }

    private static IReadOnlyList<TimetableGrid> BuildByDayOverview(TimetableDocument document)
    {
        var grids = new List<TimetableGrid>();
        var classes = document.Classes.OrderBy(c => c.Position).ToList();

        foreach (var day in document.Days.Where(d => d.IsActive).OrderBy(d => d.Weekday.DisplayIndex()))
        {
            var daySlots = document.Slots
                .Where(s => s.DayId == day.Id)
                .OrderBy(s => s.SlotIndex)
                .ToList();

            var rows = daySlots
                .Select(s => new GridRow(s.StartMinute, s.EndMinute, false, string.Empty))
                .Concat(day.Breaks.Select(b => new GridRow(b.StartMinute, b.EndMinute, true, b.Label)))
                .OrderBy(r => r.StartMinute)
                .ToList();

            var cells = new List<IReadOnlyList<GridCell>>();
            foreach (var row in rows)
            {
                var line = new List<GridCell>();
                foreach (var schoolClass in classes)
                {
                    line.Add(ResolveCell(
                        document,
                        row,
                        day,
                        slot => LessonOf(document, schoolClass.Id, slot, byTeacher: false)));
                }

                cells.Add(line);
            }

            grids.Add(new TimetableGrid(
                $"Jour — {day.Weekday.ToFrenchLabel()}",
                classes.Select(c => c.Name).ToList(),
                rows,
                cells));
        }

        return grids;
    }

    private static GridCell ResolveCell(
        TimetableDocument document,
        GridRow row,
        Domain.Entities.SchoolDay day,
        Func<Domain.Entities.TimeSlot, Domain.Entities.Lesson?> lessonSelector)
    {
        if (row.IsBreak)
        {
            return new GridCell(row.Label, null, IsBreak: true);
        }

        var slot = document.Slots.FirstOrDefault(s =>
            s.DayId == day.Id && s.StartMinute == row.StartMinute && s.EndMinute == row.EndMinute);

        if (slot is null)
        {
            return new GridCell(null, null, IsUnavailable: true);
        }

        var lesson = lessonSelector(slot);
        if (lesson is null)
        {
            return new GridCell(null, null);
        }

        var subject = document.Subjects.FirstOrDefault(s => s.Id == lesson.SubjectId);
        var teacher = document.Teachers.FirstOrDefault(t => t.Id == lesson.TeacherId);
        var schoolClass = document.Classes.FirstOrDefault(c => c.Id == lesson.ClassId);

        var text = $"{subject?.Name}\n{teacher?.DisplayName}".TrimEnd();
        if (document.Scope == ExportScope.ByTeacher)
        {
            text = $"{schoolClass?.Name}\n{subject?.Name}".TrimEnd();
        }
        else if (document.Scope == ExportScope.ByDayOverview)
        {
            text = $"{subject?.Name}\n{schoolClass?.Name}".TrimEnd();
        }

        return new GridCell(text, subject?.ColorHex ?? "#4F81BD");
    }

    private static Domain.Entities.Lesson? LessonOf(
        TimetableDocument document,
        int ownerId,
        Domain.Entities.TimeSlot slot,
        bool byTeacher)
        => document.Lessons.FirstOrDefault(l =>
            l.TimeSlotId == slot.Id && (byTeacher ? l.TeacherId == ownerId : l.ClassId == ownerId));

    private static List<GridRow> BuildRows(TimetableDocument document)
    {
        var rows = new List<GridRow>();
        var seen = new HashSet<(int, int, bool)>();

        foreach (var day in document.Days.Where(d => d.IsActive))
        {
            foreach (var slot in document.Slots.Where(s => s.DayId == day.Id))
            {
                if (seen.Add((slot.StartMinute, slot.EndMinute, false)))
                {
                    rows.Add(new GridRow(slot.StartMinute, slot.EndMinute, false, string.Empty));
                }
            }

            foreach (var breakPeriod in day.Breaks)
            {
                if (seen.Add((breakPeriod.StartMinute, breakPeriod.EndMinute, true)))
                {
                    rows.Add(new GridRow(breakPeriod.StartMinute, breakPeriod.EndMinute, true, breakPeriod.Label));
                }
            }
        }

        return rows.OrderBy(r => r.StartMinute).ThenBy(r => r.EndMinute).ToList();
    }
}
