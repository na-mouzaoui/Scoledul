using Microsoft.EntityFrameworkCore;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Domain.Common;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;
using Timetable.Infrastructure.Persistence;

namespace Timetable.Infrastructure.Repositories;

/// <summary>
/// Implémentation EF Core du dépôt de projet. Chaque opération utilise son propre contexte,
/// dans une transaction, afin de garantir l'intégrité des écritures (NFR-04).
/// </summary>
public sealed class TimetableRepository : ITimetableRepository
{
    private readonly TimetableDbContextFactory _factory;

    public TimetableRepository(TimetableDbContextFactory factory) => _factory = factory;

    public string FilePath => _factory.FilePath;

    public async Task<ProjectSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();

        var settings = await db.ProjectSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var days = await db.SchoolDays.AsNoTracking()
            .Include(d => d.Breaks)
            .Include(d => d.Slots)
            .OrderBy(d => d.Weekday)
            .ToListAsync(cancellationToken);

        foreach (var day in days)
        {
            foreach (var slot in day.Slots)
            {
                slot.Day = day;
            }
        }

        var slots = days
            .SelectMany(d => d.Slots)
            .OrderBy(s => (int)s.Weekday)
            .ThenBy(s => s.SlotIndex)
            .ToList();

        var classes = await db.SchoolClasses.AsNoTracking().OrderBy(c => c.Position).ToListAsync(cancellationToken);
        var subjects = await db.Subjects.AsNoTracking().OrderBy(s => s.Id).ToListAsync(cancellationToken);
        var teachers = await db.Teachers.AsNoTracking()
            .Include(t => t.Unavailabilities)
            .OrderBy(t => t.LastName)
            .ToListAsync(cancellationToken);
        var requirements = await db.ClassSubjectRequirements.AsNoTracking().ToListAsync(cancellationToken);
        var versions = await db.TimetableVersions.AsNoTracking().OrderBy(v => v.Id).ToListAsync(cancellationToken);
        var lessons = await db.Lessons.AsNoTracking().ToListAsync(cancellationToken);
        var preferences = await db.GenerationPreferences.AsNoTracking().ToListAsync(cancellationToken);

        return new ProjectSnapshot
        {
            FilePath = FilePath,
            Name = settings.GetValueOrDefault(ProjectSettingKeys.Name, "Nouveau projet"),
            Settings = settings,
            SlotDurationMinutes = ParseInt(settings.GetValueOrDefault(ProjectSettingKeys.SlotDurationMinutes), 60),
            OneClassPerTeacherWeek = settings.GetValueOrDefault(ProjectSettingKeys.OneClassPerTeacherWeek) == "1",
            Days = days,
            Slots = slots,
            Classes = classes,
            Subjects = subjects,
            Teachers = teachers,
            Requirements = requirements,
            Versions = versions,
            Lessons = lessons,
            Weights = ToWeights(preferences),
        };
    }

    public async Task SaveSettingsAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        foreach (var (key, value) in settings)
        {
            var existing = await db.ProjectSettings.FindAsync(new object[] { key }, cancellationToken);
            if (existing is null)
            {
                db.ProjectSettings.Add(new ProjectSetting { Key = key, Value = value });
            }
            else
            {
                existing.Value = value;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveWeekAsync(
        IReadOnlyList<SchoolDay> days,
        IReadOnlyList<TimeSlot> slots,
        CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.SchoolDays
            .Include(d => d.Breaks)
            .Include(d => d.Slots)
            .ToListAsync(cancellationToken);

        var trackedByWeekday = new Dictionary<Weekday, SchoolDay>();

        foreach (var incoming in days)
        {
            var tracked = existing.FirstOrDefault(d => d.Weekday == incoming.Weekday);
            if (tracked is null)
            {
                tracked = new SchoolDay { Weekday = incoming.Weekday };
                db.SchoolDays.Add(tracked);
            }

            tracked.IsActive = incoming.IsActive;
            tracked.StartMinute = incoming.StartMinute;
            tracked.EndMinute = incoming.EndMinute;

            db.BreakPeriods.RemoveRange(tracked.Breaks.ToList());
            tracked.Breaks.Clear();
            db.TimeSlots.RemoveRange(tracked.Slots.ToList());
            tracked.Slots.Clear();

            trackedByWeekday[incoming.Weekday] = tracked;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var slot in slots)
        {
            var weekday = slot.Day?.Weekday ?? slot.Weekday;
            if (!trackedByWeekday.TryGetValue(weekday, out var tracked))
            {
                tracked = existing.FirstOrDefault(d => d.Id == slot.DayId);
                if (tracked is null)
                {
                    continue;
                }
            }

            db.TimeSlots.Add(new TimeSlot
            {
                DayId = tracked.Id,
                SlotIndex = slot.SlotIndex,
                StartMinute = slot.StartMinute,
                EndMinute = slot.EndMinute,
                FollowsBreak = slot.FollowsBreak,
            });
        }

        foreach (var incoming in days)
        {
            var tracked = trackedByWeekday[incoming.Weekday];
            foreach (var breakPeriod in incoming.Breaks)
            {
                db.BreakPeriods.Add(new BreakPeriod
                {
                    DayId = tracked.Id,
                    Label = breakPeriod.Label,
                    StartMinute = breakPeriod.StartMinute,
                    EndMinute = breakPeriod.EndMinute,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<SchoolClass> SaveClassAsync(SchoolClass entity, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        var tracked = entity.Id == 0
            ? null
            : await db.SchoolClasses.FindAsync(new object[] { entity.Id }, cancellationToken);

        if (tracked is null)
        {
            tracked = new SchoolClass();
            db.SchoolClasses.Add(tracked);
        }

        tracked.Name = entity.Name;
        tracked.Position = entity.Position;
        await db.SaveChangesAsync(cancellationToken);

        entity.Id = tracked.Id;
        return entity;
    }

    public async Task DeleteClassAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await db.SchoolClasses.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<Subject> SaveSubjectAsync(Subject entity, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        var tracked = entity.Id == 0
            ? null
            : await db.Subjects.FindAsync(new object[] { entity.Id }, cancellationToken);

        if (tracked is null)
        {
            tracked = new Subject();
            db.Subjects.Add(tracked);
        }

        tracked.Name = entity.Name;
        tracked.ColorHex = entity.ColorHex;
        tracked.BlockSize = entity.BlockSize;
        await db.SaveChangesAsync(cancellationToken);

        entity.Id = tracked.Id;
        return entity;
    }

    public async Task DeleteSubjectAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await db.Subjects.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<Teacher> SaveTeacherAsync(Teacher entity, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        var tracked = entity.Id == 0
            ? null
            : await db.Teachers.FindAsync(new object[] { entity.Id }, cancellationToken);

        if (tracked is null)
        {
            tracked = new Teacher();
            db.Teachers.Add(tracked);
        }

        tracked.LastName = entity.LastName;
        tracked.FirstName = entity.FirstName;
        tracked.SubjectId = entity.SubjectId;
        await db.SaveChangesAsync(cancellationToken);

        entity.Id = tracked.Id;
        return entity;
    }

    public async Task DeleteTeacherAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await db.Teachers.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SetTeacherUnavailabilitiesAsync(
        int teacherId,
        IReadOnlyCollection<int> slotIds,
        CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.TeacherUnavailabilities
            .Where(u => u.TeacherId == teacherId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var slotId in slotIds.Distinct())
        {
            db.TeacherUnavailabilities.Add(new TeacherUnavailability
            {
                TeacherId = teacherId,
                TimeSlotId = slotId,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveRequirementsAsync(
        int classId,
        IReadOnlyList<ClassSubjectRequirement> requirements,
        CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.ClassSubjectRequirements
            .Where(r => r.ClassId == classId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var requirement in requirements)
        {
            db.ClassSubjectRequirements.Add(new ClassSubjectRequirement
            {
                ClassId = classId,
                SubjectId = requirement.SubjectId,
                WeeklySessions = requirement.WeeklySessions,
                FixedTeacherId = requirement.FixedTeacherId,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SavePreferencesAsync(GenerationWeights weights, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        foreach (var (code, weight) in weights.Enumerate())
        {
            var key = PreferenceCode(code);
            var existing = await db.GenerationPreferences.FindAsync(new object[] { key }, cancellationToken);
            if (existing is null)
            {
                db.GenerationPreferences.Add(new GenerationPreference { Code = key, Weight = weight });
            }
            else
            {
                existing.Weight = weight;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TimetableVersion> SaveVersionAsync(
        TimetableVersion version,
        CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        var tracked = version.Id == 0
            ? null
            : await db.TimetableVersions.FindAsync(new object[] { version.Id }, cancellationToken);

        if (tracked is null)
        {
            tracked = new TimetableVersion();
            db.TimetableVersions.Add(tracked);
        }

        tracked.Name = version.Name;
        tracked.CreatedAtUtc = version.CreatedAtUtc;
        tracked.IsSelected = version.IsSelected;
        tracked.SolverSeed = version.SolverSeed;
        tracked.Score = version.Score;
        await db.SaveChangesAsync(cancellationToken);

        version.Id = tracked.Id;
        return version;
    }

    public async Task SaveLessonsAsync(
        int versionId,
        IReadOnlyList<Lesson> lessons,
        CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Lessons.Where(l => l.VersionId == versionId).ExecuteDeleteAsync(cancellationToken);

        foreach (var lesson in lessons)
        {
            db.Lessons.Add(new Lesson
            {
                VersionId = versionId,
                ClassId = lesson.ClassId,
                SubjectId = lesson.SubjectId,
                TeacherId = lesson.TeacherId,
                TimeSlotId = lesson.TimeSlotId,
                BlockKey = lesson.BlockKey,
                IsLocked = lesson.IsLocked,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetVersionSelectedAsync(int versionId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await db.TimetableVersions.ExecuteUpdateAsync(
            setters => setters.SetProperty(v => v.IsSelected, v => v.Id == versionId),
            cancellationToken);
    }

    public async Task DeleteVersionAsync(int versionId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.Create();
        await db.TimetableVersions.Where(v => v.Id == versionId).ExecuteDeleteAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

    private static string PreferenceCode(SoftConstraintCode code) => $"SC-{(int)code:00}";

    private static GenerationWeights ToWeights(IReadOnlyList<GenerationPreference> preferences)
    {
        var weights = new GenerationWeights();
        foreach (var preference in preferences)
        {
            if (preference.Code.StartsWith("SC-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(preference.Code.AsSpan(3), out var index) &&
                Enum.IsDefined(typeof(SoftConstraintCode), index))
            {
                weights[(SoftConstraintCode)index] = preference.Weight;
            }
        }

        return weights;
    }
}
