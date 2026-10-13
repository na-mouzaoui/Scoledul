using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Timetable.Infrastructure.Persistence;

/// <summary>Fabrique de contextes EF Core pour un fichier projet donné.</summary>
public sealed class TimetableDbContextFactory
{
    private readonly DbContextOptions<TimetableDbContext> _options;

    public TimetableDbContextFactory(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();

        var builder = new DbContextOptionsBuilder<TimetableDbContext>();
        builder.UseSqlite(connectionString, sqlite => sqlite.CommandTimeout(120));
        builder.AddInterceptors(new SqlitePragmaInterceptor());
        _options = builder.Options;
    }

    public string FilePath { get; }

    public TimetableDbContext Create() => new(_options);
}
