using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Timetable.Domain.Entities;

namespace Timetable.Infrastructure.Persistence;

/// <summary>
/// Contexte Entity Framework Core du fichier projet <c>.edt</c> (§10).
/// Le schéma reprend exactement les contraintes, clés étrangères et index du cahier des charges.
/// </summary>
public class TimetableDbContext : DbContext
{
    public TimetableDbContext(DbContextOptions<TimetableDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjectSetting> ProjectSettings => Set<ProjectSetting>();

    public DbSet<SchoolDay> SchoolDays => Set<SchoolDay>();

    public DbSet<BreakPeriod> BreakPeriods => Set<BreakPeriod>();

    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<TeacherUnavailability> TeacherUnavailabilities => Set<TeacherUnavailability>();

    public DbSet<ClassSubjectRequirement> ClassSubjectRequirements => Set<ClassSubjectRequirement>();

    public DbSet<TimetableVersion> TimetableVersions => Set<TimetableVersion>();

    public DbSet<Lesson> Lessons => Set<Lesson>();

    public DbSet<GenerationPreference> GenerationPreferences => Set<GenerationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProjectSettings(modelBuilder);
        ConfigureSchoolDay(modelBuilder);
        ConfigureBreakPeriod(modelBuilder);
        ConfigureTimeSlot(modelBuilder);
        ConfigureSchoolClass(modelBuilder);
        ConfigureSubject(modelBuilder);
        ConfigureTeacher(modelBuilder);
        ConfigureTeacherUnavailability(modelBuilder);
        ConfigureRequirement(modelBuilder);
        ConfigureVersion(modelBuilder);
        ConfigureLesson(modelBuilder);
        ConfigureGenerationPreference(modelBuilder);
    }

    private static void ConfigureProjectSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectSetting>(entity =>
        {
            entity.ToTable("project_setting");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value").IsRequired();
        });
    }

    private static void ConfigureSchoolDay(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolDay>(entity =>
        {
            entity.ToTable("school_day", table =>
            {
                table.HasCheckConstraint("ck_school_day_weekday", "weekday BETWEEN 1 AND 7");
                table.HasCheckConstraint("ck_school_day_active", "is_active IN (0, 1)");
                table.HasCheckConstraint(
                    "ck_school_day_minutes",
                    "start_minute BETWEEN 0 AND 1439 AND end_minute BETWEEN 1 AND 1440 AND end_minute > start_minute");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Weekday).HasColumnName("weekday").HasConversion<int>();
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.StartMinute).HasColumnName("start_minute");
            entity.Property(e => e.EndMinute).HasColumnName("end_minute");
            entity.HasIndex(e => e.Weekday).IsUnique();
        });
    }

    private static void ConfigureBreakPeriod(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BreakPeriod>(entity =>
        {
            entity.ToTable("break_period", table =>
                table.HasCheckConstraint("ck_break_period_minutes", "end_minute > start_minute"));

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DayId).HasColumnName("day_id");
            entity.Property(e => e.Label).HasColumnName("label").IsRequired();
            entity.Property(e => e.StartMinute).HasColumnName("start_minute");
            entity.Property(e => e.EndMinute).HasColumnName("end_minute");

            entity.HasOne(e => e.Day)
                .WithMany(d => d.Breaks)
                .HasForeignKey(e => e.DayId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTimeSlot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeSlot>(entity =>
        {
            entity.ToTable("time_slot", table =>
                table.HasCheckConstraint(
                    "ck_time_slot_follows_break", "follows_break IN (0, 1)"));

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DayId).HasColumnName("day_id");
            entity.Property(e => e.SlotIndex).HasColumnName("slot_index");
            entity.Property(e => e.StartMinute).HasColumnName("start_minute");
            entity.Property(e => e.EndMinute).HasColumnName("end_minute");
            entity.Property(e => e.FollowsBreak).HasColumnName("follows_break");
            entity.Ignore(e => e.Weekday);
            entity.HasIndex(e => new { e.DayId, e.SlotIndex }).IsUnique();

            entity.HasOne(e => e.Day)
                .WithMany(d => d.Slots)
                .HasForeignKey(e => e.DayId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSchoolClass(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("school_class");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Position).HasColumnName("position");
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }

    private static void ConfigureSubject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("subject", table =>
                table.HasCheckConstraint("ck_subject_block_size", "block_size BETWEEN 1 AND 4"));

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.ColorHex).HasColumnName("color_hex").IsRequired();
            entity.Property(e => e.BlockSize).HasColumnName("block_size");
            entity.Ignore(e => e.RequiresBlocks);
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }

    private static void ConfigureTeacher(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.ToTable("teacher");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.LastName).HasColumnName("last_name").IsRequired();
            entity.Property(e => e.FirstName).HasColumnName("first_name").IsRequired();
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Ignore(e => e.FullName);
            entity.Ignore(e => e.DisplayName);
            entity.HasIndex(e => new { e.LastName, e.FirstName, e.SubjectId }).IsUnique();
            entity.HasIndex(e => e.SubjectId).HasDatabaseName("ix_teacher_subject");

            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTeacherUnavailability(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeacherUnavailability>(entity =>
        {
            entity.ToTable("teacher_unavailability");
            entity.HasKey(e => new { e.TeacherId, e.TimeSlotId });
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.TimeSlotId).HasColumnName("time_slot_id");

            entity.HasOne(e => e.Teacher)
                .WithMany(t => t.Unavailabilities)
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TimeSlot)
                .WithMany()
                .HasForeignKey(e => e.TimeSlotId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRequirement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClassSubjectRequirement>(entity =>
        {
            entity.ToTable("class_subject_requirement", table =>
                table.HasCheckConstraint("ck_requirement_weekly", "weekly_sessions >= 0"));

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.WeeklySessions).HasColumnName("weekly_sessions");
            entity.Property(e => e.FixedTeacherId).HasColumnName("fixed_teacher_id");
            entity.HasIndex(e => new { e.ClassId, e.SubjectId }).IsUnique();

            entity.HasOne(e => e.Class)
                .WithMany()
                .HasForeignKey(e => e.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FixedTeacher)
                .WithMany()
                .HasForeignKey(e => e.FixedTeacherId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureVersion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimetableVersion>(entity =>
        {
            entity.ToTable("timetable_version", table =>
                table.HasCheckConstraint("ck_version_selected", "is_selected IN (0, 1)"));

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("created_at")
                .HasConversion(
                    value => value.ToString("o", CultureInfo.InvariantCulture),
                    value => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            entity.Property(e => e.IsSelected).HasColumnName("is_selected");
            entity.Property(e => e.SolverSeed).HasColumnName("solver_seed");
            entity.Property(e => e.Score).HasColumnName("score");
        });
    }

    private static void ConfigureLesson(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.ToTable("lesson", table =>
                table.HasCheckConstraint("ck_lesson_locked", "is_locked IN (0, 1)"));

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VersionId).HasColumnName("version_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.TimeSlotId).HasColumnName("time_slot_id");
            entity.Property(e => e.BlockKey).HasColumnName("block_key");
            entity.Property(e => e.IsLocked).HasColumnName("is_locked");

            entity.HasIndex(e => new { e.VersionId, e.ClassId, e.TimeSlotId })
                .IsUnique()
                .HasDatabaseName("ux_lesson_version_class_slot");

            entity.HasIndex(e => new { e.VersionId, e.TeacherId, e.TimeSlotId })
                .IsUnique()
                .HasDatabaseName("ux_lesson_version_teacher_slot");

            entity.HasIndex(e => new { e.VersionId, e.ClassId }).HasDatabaseName("ix_lesson_version_class");
            entity.HasIndex(e => new { e.VersionId, e.TeacherId }).HasDatabaseName("ix_lesson_version_teacher");

            entity.HasOne(e => e.Version)
                .WithMany(v => v.Lessons)
                .HasForeignKey(e => e.VersionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Class)
                .WithMany()
                .HasForeignKey(e => e.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TimeSlot)
                .WithMany()
                .HasForeignKey(e => e.TimeSlotId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGenerationPreference(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GenerationPreference>(entity =>
        {
            entity.ToTable("generation_preference", table =>
                table.HasCheckConstraint("ck_preference_weight", "weight BETWEEN 0 AND 10"));

            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Weight).HasColumnName("weight");
        });
    }
}
