using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

/// <summary>
/// A real SQLite database that lives only in memory while the test runs.
/// The migrations are applied, so tests check the same schema the app uses.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();

        using var context = CreateContext();
        context.Database.Migrate();
    }

    public AssistantDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AssistantDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
