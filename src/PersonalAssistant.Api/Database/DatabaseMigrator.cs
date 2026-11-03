using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Api.Database;

/// <summary>
/// Applies pending EF Core migrations when the app starts.
/// </summary>
public sealed class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();

        EnsureDatabaseFolderExists(context.Database.GetConnectionString());
        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database is up to date");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void EnsureDatabaseFolderExists(string? connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(dataSource));

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }
}
