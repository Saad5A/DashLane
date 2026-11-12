using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Data;

public static class DatabaseInitializer
{
    private static readonly (string Name, string Type)[] AddedColumns =
    {
        ("GoogleSubjectId", "TEXT NULL"),
        ("DocumentCheckDecision", "TEXT NULL"),
        ("DocumentCheckSummary", "TEXT NULL"),
        ("DocumentCheckId", "TEXT NULL"),
        ("DocumentCheckedUtc", "TEXT NULL"),
    };

    /// <summary>Columns that older databases created as NOT NULL but the model now allows to be empty.</summary>
    private static readonly string[] NowNullable = { "PasswordHash" };

    public static async Task InitializeAsync(DashLaneContext db)
    {
        await db.Database.EnsureCreatedAsync();

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            var columns = await ReadColumnsAsync(connection);

            foreach (var (name, type) in AddedColumns.Where(column => !columns.ContainsKey(column.Name)))
            {
                await ExecuteAsync(connection, $"ALTER TABLE \"Members\" ADD COLUMN \"{name}\" {type}");
            }

            if (NowNullable.Any(name => columns.TryGetValue(name, out var notNull) && notNull))
            {
                await RelaxNotNullAsync(connection);
            }

            await ExecuteAsync(connection,
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Members_GoogleSubjectId\" ON \"Members\" (\"GoogleSubjectId\") WHERE \"GoogleSubjectId\" IS NOT NULL");

            // EnsureCreated only initializes empty databases. Apply missing SQLite tables and indexes
            // from the EF model after upgrading the existing Members table.
            var schema = db.Database.GenerateCreateScript()
                .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ")
                .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ");
            await using var transaction = await connection.BeginTransactionAsync();
            var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var statement in statements.Where(sql => sql.StartsWith("CREATE TABLE ")))
                await ExecuteAsync(connection, statement, transaction);
            foreach (var (table, column, type) in new[]
            {
                ("Vehicles", "Year", "INTEGER"),
                ("DriverProfiles", "LicenseNumber", "TEXT"),
                ("DriverProfiles", "LicenseExpiry", "TEXT"),
                ("DriverProfiles", "LicenseDocumentUrl", "TEXT"),
                ("RideBookings", "QuotedFare", "TEXT"),
                ("RideBookings", "IncludedTax", "TEXT"),
                ("RideBookings", "PickupInstructions", "TEXT"),
                ("RideBookings", "DriverNote", "TEXT"),
                ("RideBookings", "PickupEtaUtc", "TEXT"),
                ("RideBookings", "ArrivedUtc", "TEXT"),
                ("RideBookings", "CancelledUtc", "TEXT"),
                ("RideBookings", "CancellationFee", "TEXT"),
                ("ReviewFeedback", "IsDriver", "INTEGER NOT NULL DEFAULT 0"),
                ("PaymentTransactions", "Discount", "TEXT"),
                ("Promotions", "EligibleRiderId", "INTEGER")
            })
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
                if (Convert.ToInt64(await command.ExecuteScalarAsync()) == 0)
                    await ExecuteAsync(connection, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}", transaction);
            }
            await ExecuteAsync(connection, "DROP INDEX IF EXISTS \"IX_ReviewFeedback_RideBookingId\"", transaction);
            foreach (var statement in statements.Where(sql => !sql.StartsWith("CREATE TABLE ")))
                await ExecuteAsync(connection, statement, transaction);
            await transaction.CommitAsync();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<Dictionary<string, bool>> ReadColumnsAsync(DbConnection connection)
    {
        var columns = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"Members\")";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(1)] = reader.GetInt64(3) == 1;
        }

        return columns;
    }

    /// <summary>
    /// SQLite can't change a column's NOT NULL constraint in place, so rebuild the table:
    /// copy the current definition with the constraint relaxed, move the rows, and restore the indexes.
    /// </summary>
    private static async Task RelaxNotNullAsync(DbConnection connection)
    {
        string createSql;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'Members'";
            createSql = (string)(await command.ExecuteScalarAsync())!;
        }

        var indexSql = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'index' AND tbl_name = 'Members' AND sql IS NOT NULL";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexSql.Add(reader.GetString(0));
            }
        }

        var rebuilt = createSql.Replace("CREATE TABLE \"Members\"", "CREATE TABLE \"Members_rebuild\"");
        foreach (var name in NowNullable)
        {
            rebuilt = rebuilt.Replace($"\"{name}\" TEXT NOT NULL", $"\"{name}\" TEXT NULL");
        }

        var columnList = string.Join(", ", (await ReadColumnsAsync(connection)).Keys.Select(name => $"\"{name}\""));

        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF");
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await ExecuteAsync(connection, rebuilt, transaction);
            await ExecuteAsync(connection, $"INSERT INTO \"Members_rebuild\" ({columnList}) SELECT {columnList} FROM \"Members\"", transaction);
            await ExecuteAsync(connection, "DROP TABLE \"Members\"", transaction);
            await ExecuteAsync(connection, "ALTER TABLE \"Members_rebuild\" RENAME TO \"Members\"", transaction);
            foreach (var sql in indexSql)
            {
                await ExecuteAsync(connection, sql, transaction);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await ExecuteAsync(connection, "PRAGMA foreign_keys = ON");
        }
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync();
    }
}
