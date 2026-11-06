using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(DashLaneContext db)
    {
        await db.Database.EnsureCreatedAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Members\" ADD COLUMN \"GoogleSubjectId\" TEXT NULL");
        }
        catch (SqliteException exception) when (exception.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
        }

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Members_GoogleSubjectId\" ON \"Members\" (\"GoogleSubjectId\") WHERE \"GoogleSubjectId\" IS NOT NULL");
    }
}
