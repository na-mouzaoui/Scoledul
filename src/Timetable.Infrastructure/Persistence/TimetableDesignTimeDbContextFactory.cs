using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Timetable.Infrastructure.Persistence;

/// <summary>
/// Fabrique de contexte au moment de la conception, utilisée par <c>dotnet ef</c> pour créer les migrations.
/// Le fichier cible n'est jamais réellement utilisé.
/// </summary>
public sealed class TimetableDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimetableDbContext>
{
    public TimetableDbContext CreateDbContext(string[] args)
    {
        var directory = Path.Combine(Path.GetTempPath(), "timetable-design");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "design.edt");
        return new TimetableDbContextFactory(path).Create();
    }
}
