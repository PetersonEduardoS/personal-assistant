using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PersonalAssistant.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string VerifyToken = "test-verify-token";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:VerifyToken"] = VerifyToken
            });
        });
    }
}
