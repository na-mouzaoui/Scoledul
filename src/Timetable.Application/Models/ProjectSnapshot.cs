using Timetable.Application.Models;
using Timetable.Domain.Common;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Application.Models;

/// <summary>Vue complète et immuable du contenu d'un projet.</summary>
public sealed class ProjectSnapshot
{
    public string? FilePath { get; init; }

    public string Name { get; init; } = "Nouveau projet";

    public IReadOnlyDictionary<string, string> Settings { get; init; }
        = new Dictionary<string, string>();

    public int SlotDurationMinutes { get; init; } = 60;

    public bool OneClassPerTeacherWeek { get; init; }

    public IReadOnlyList<SchoolDay> Days { get; init; } = Array.Empty<SchoolDay>();

    public IReadOnlyList<TimeSlot> Slots { get; init; } = Array.Empty<TimeSlot>();

    public IReadOnlyList<SchoolClass> Classes { get; init; } = Array.Empty<SchoolClass>();

    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();

    public IReadOnlyList<Teacher> Teachers { get; init; } = Array.Empty<Teacher>();

    public IReadOnlyList<ClassSubjectRequirement> Requirements { get; init; }
        = Array.Empty<ClassSubjectRequirement>();

    public IReadOnlyList<TimetableVersion> Versions { get; init; } = Array.Empty<TimetableVersion>();

    public IReadOnlyList<Lesson> Lessons { get; init; } = Array.Empty<Lesson>();

    public GenerationWeights Weights { get; init; } = GenerationWeights.Default;

    public static ProjectSnapshot Empty { get; } = new();

    public IReadOnlyList<Weekday> ActiveWeekdays
        => Days.Where(d => d.IsActive).Select(d => d.Weekday).OrderBy(w => w).ToList();

    public IReadOnlySet<(int TeacherId, int SlotId)> UnavailabilityPairs
        => Teachers
            .SelectMany(t => t.Unavailabilities.Select(u => (t.Id, u.TimeSlotId)))
            .ToHashSet();

    public TimetableVersion? SelectedVersion
        => Versions.FirstOrDefault(v => v.IsSelected) ?? (Versions.Count > 0 ? Versions[0] : null);

    public IReadOnlyList<Lesson> LessonsForVersion(int versionId)
        => Lessons.Where(l => l.VersionId == versionId).ToList();

    /// <summary>Construit le contexte de validation à partir du contenu du projet.</summary>
    public ScheduleContext ToScheduleContext() => new()
    {
        Slots = Slots,
        Classes = Classes,
        Subjects = Subjects,
        Teachers = Teachers,
        Requirements = Requirements,
        Unavailabilities = UnavailabilityPairs,
        OneClassPerTeacherWeek = OneClassPerTeacherWeek,
    };
}

/// <summary>Entrée d'un projet récent.</summary>
public sealed record RecentProject(string FilePath, string Name, DateTime LastOpenedUtc);
