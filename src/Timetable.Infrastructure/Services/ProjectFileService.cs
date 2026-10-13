using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Application.Services;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;
using Timetable.Infrastructure.Persistence;
using Timetable.Infrastructure.Repositories;

namespace Timetable.Infrastructure.Services;

/// <summary>
/// Gestion des fichiers projet <c>.edt</c> : création, ouverture (avec sauvegarde et migration),
/// duplication et liste des projets récents (FR-01, NFR-11).
/// </summary>
public sealed class ProjectFileService : IProjectFileService
{
    private readonly IAppSettingsService _settings;
    private readonly ISlotGenerator _slotGenerator;

    public ProjectFileService(IAppSettingsService settings, ISlotGenerator? slotGenerator = null)
    {
        _settings = settings;
        _slotGenerator = slotGenerator ?? new SlotGenerator();
    }

    public string DefaultProjectsDirectory => _settings.Current.DefaultProjectsDirectory;

    public IReadOnlyList<RecentProject> GetRecentProjects()
        => _settings.Current.RecentProjects
            .Where(r => File.Exists(r.FilePath))
            .OrderByDescending(r => r.LastOpenedUtc)
            .Take(10)
            .ToList();

    public void RememberProject(string filePath, string? name = null)
    {
        var full = Path.GetFullPath(filePath);
        var list = _settings.Current.RecentProjects;
        list.RemoveAll(r => string.Equals(r.FilePath, full, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, new RecentProject(
            full,
            name ?? Path.GetFileNameWithoutExtension(full),
            DateTime.UtcNow));

        if (list.Count > 15)
        {
            list.RemoveRange(15, list.Count - 15);
        }

        _settings.Save();
    }

    public async Task<ITimetableRepository> CreateNewAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        DeleteSidecarFiles(full);
        if (File.Exists(full))
        {
            File.Delete(full);
        }

        var factory = new TimetableDbContextFactory(full);
        await using (var db = factory.Create())
        {
            await db.Database.MigrateAsync(cancellationToken);
            await SeedAsync(db, cancellationToken);
        }

        RememberProject(full, Path.GetFileNameWithoutExtension(full));
        return new TimetableRepository(factory);
    }

    public async Task<ITimetableRepository> OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(filePath);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException("Le fichier projet est introuvable.", full);
        }

        BackupBeforeMigration(full);

        var factory = new TimetableDbContextFactory(full);
        await using (var db = factory.Create())
        {
            await db.Database.MigrateAsync(cancellationToken);
            await EnsureSlotsAsync(db, cancellationToken);
        }

        RememberProject(full, Path.GetFileNameWithoutExtension(full));
        return new TimetableRepository(factory);
    }

    public Task DuplicateAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);

        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Le projet à dupliquer est introuvable.", source);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        DeleteSidecarFiles(destination);
        File.Copy(source, destination, overwrite: true);

        RememberProject(destination, Path.GetFileNameWithoutExtension(destination));
        return Task.CompletedTask;
    }

    private void BackupBeforeMigration(string fullPath)
    {
        var copies = Math.Max(1, _settings.Current.AutoSaveCopies);
        var backupDirectory = Path.Combine(Path.GetDirectoryName(fullPath)!, ".backups");
        Directory.CreateDirectory(backupDirectory);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var destination = Path.Combine(backupDirectory, $"{name}-{stamp}.edt");

        try
        {
            File.Copy(fullPath, destination, overwrite: true);

            var stale = Directory.GetFiles(backupDirectory, $"{name}-*.edt")
                .OrderByDescending(File.GetCreationTimeUtc)
                .Skip(copies)
                .ToList();

            foreach (var file in stale)
            {
                File.Delete(file);
            }
        }
        catch (IOException)
        {
            // Une sauvegarde préalable manquée ne doit pas empêcher l'ouverture.
        }
    }

    private static void DeleteSidecarFiles(string path)
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = path + suffix;
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }
    }

    private async Task SeedAsync(TimetableDbContext db, CancellationToken cancellationToken)
    {
        var global = _settings.Current.GlobalWeek;
        var days = ProjectDefaults.Days().ToList();
        var duration = 60;

        if (global is not null && global.Days.Count > 0)
        {
            var (configuredDays, configuredDuration) = WeekConfigurationMapper.ToModels(global);
            days = configuredDays;
            duration = configuredDuration > 0 ? configuredDuration : 60;
        }

        db.ProjectSettings.AddRange(
            new ProjectSetting { Key = ProjectSettingKeys.Name, Value = "Nouveau projet" },
            new ProjectSetting
            {
                Key = ProjectSettingKeys.SlotDurationMinutes,
                Value = duration.ToString(CultureInfo.InvariantCulture),
            },
            new ProjectSetting { Key = ProjectSettingKeys.OneClassPerTeacherWeek, Value = "0" },
            new ProjectSetting { Key = ProjectSettingKeys.SchemaVersion, Value = "1" });

        db.SchoolDays.AddRange(days);

        db.Subjects.AddRange(ProjectDefaults.Subjects.Select(s => new Subject
        {
            Name = s.Name,
            ColorHex = s.Color,
            BlockSize = s.BlockSize,
        }));

        var weights = GenerationWeights.Default;
        foreach (var (code, weight) in weights.Enumerate())
        {
            db.GenerationPreferences.Add(new GenerationPreference
            {
                Code = $"SC-{(int)code:00}",
                Weight = weight,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        // Les créneaux doivent exister dès la création : sans eux, la génération produit un emploi du temps vide.
        await GenerateSlotsAsync(db, days, duration, cancellationToken);
    }

    /// <summary>Génère et persiste la grille de créneaux d'un projet (FR-04).</summary>
    private async Task GenerateSlotsAsync(
        TimetableDbContext db,
        IReadOnlyList<SchoolDay> days,
        int slotDurationMinutes,
        CancellationToken cancellationToken)
    {
        if (slotDurationMinutes <= 0)
        {
            slotDurationMinutes = 60;
        }

        var generated = _slotGenerator.Generate(days, slotDurationMinutes);
        foreach (var slot in generated.Slots)
        {
            db.TimeSlots.Add(new TimeSlot
            {
                DayId = slot.DayId,
                SlotIndex = slot.SlotIndex,
                StartMinute = slot.StartMinute,
                EndMinute = slot.EndMinute,
                FollowsBreak = slot.FollowsBreak,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// S'assure qu'un projet possède des créneaux. Utile pour les projets créés par une version
    /// antérieure de l'application (jours définis mais aucune grille générée).
    /// </summary>
    private async Task EnsureSlotsAsync(TimetableDbContext db, CancellationToken cancellationToken)
    {
        if (await db.TimeSlots.AnyAsync(cancellationToken))
        {
            return;
        }

        var days = await db.SchoolDays
            .Include(d => d.Breaks)
            .ToListAsync(cancellationToken);

        if (days.Count == 0 || days.All(d => !d.IsActive))
        {
            return;
        }

        var duration = 60;
        var setting = await db.ProjectSettings
            .FirstOrDefaultAsync(s => s.Key == ProjectSettingKeys.SlotDurationMinutes, cancellationToken);
        if (setting is not null && int.TryParse(setting.Value, out var parsed) && parsed > 0)
        {
            duration = parsed;
        }

        await GenerateSlotsAsync(db, days, duration, cancellationToken);
    }
}
