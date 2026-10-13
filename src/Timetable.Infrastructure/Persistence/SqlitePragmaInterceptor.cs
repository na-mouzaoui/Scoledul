using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Timetable.Infrastructure.Persistence;

/// <summary>
/// Applique les PRAGMA SQLite requis à l'ouverture de chaque connexion (NFR-05) :
/// activation des clés étrangères et journalisation en mode WAL (NFR-04).
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Apply(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    private static void Apply(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();
    }
}
