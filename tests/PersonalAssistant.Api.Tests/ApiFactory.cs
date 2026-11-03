using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalAssistant.Core.Messaging;

namespace PersonalAssistant.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string VerifyToken = "test-verify-token";
    public const string AppSecret = "test-app-secret";
    public const string AllowedSender = "353800000001";

    public FakeIncomingMessageQueue Queue { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:VerifyToken"] = VerifyToken,
                ["WhatsApp:AppSecret"] = AppSecret,
                ["WhatsApp:AllowedSenders:0"] = AllowedSender
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIncomingMessageQueue>();
            services.AddSingleton<IIncomingMessageQueue>(Queue);
        });
    }
}
