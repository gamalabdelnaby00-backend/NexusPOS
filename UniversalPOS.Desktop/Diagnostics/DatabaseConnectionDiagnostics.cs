
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Diagnostics;

public static class DatabaseConnectionDiagnostics
{
    public static async Task<string> GetConnectionInfoAsync(
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var connection = dbContext.Database.GetDbConnection();

        await dbContext.Database.OpenConnectionAsync();

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT
                    DB_NAME() AS DatabaseName,
                    @@SERVERNAME AS ServerName,
                    COL_LENGTH(
                        'dbo.Products',
                        'UnitOfMeasure'
                    ) AS UnitOfMeasureColumn;
                """;

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException(
                    "The database diagnostic query returned no results.");
            }

            string databaseName =
                reader["DatabaseName"]?.ToString() ?? "Unknown";

            string serverName =
                reader["ServerName"]?.ToString() ?? "Unknown";

            bool hasUnitOfMeasure =
                reader["UnitOfMeasureColumn"] != DBNull.Value;

            return
                $"Database: {databaseName}\n" +
                $"Server: {serverName}\n" +
                $"Products.UnitOfMeasure exists: " +
                $"{(hasUnitOfMeasure ? "Yes" : "No")}";
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
