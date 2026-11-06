using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string VerifyToken = "test-verify-token";
    public const string AppSecret = "test-app-secret";
    public const string AllowedSender = "353800000001";

    public FakeIncomingMessageQueue Queue { get; } = new();
    public string? MorningBriefTestKey { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:VerifyToken"] = VerifyToken,
                ["WhatsApp:AppSecret"] = AppSecret,
                ["Users:0:PhoneNumber"] = AllowedSender,
                ["Users:0:Name"] = "Test User",
                ["MorningBriefs:TestKey"] = MorningBriefTestKey
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIncomingMessageQueue>();
            services.AddSingleton<IIncomingMessageQueue>(Queue);

            // Use an in-memory SQLite database instead of a file.
            services.RemoveAll<DbContextOptions<AssistantDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AssistantDbContext>>();
            services.AddSingleton(_ =>
            {
                var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                return connection;
            });
            services.AddDbContext<AssistantDbContext>((serviceProvider, options) =>
                options.UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>()));
        });
    }
}
