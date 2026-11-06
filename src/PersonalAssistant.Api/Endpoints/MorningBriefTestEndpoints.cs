using System.Security.Cryptography;
using System.Text;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Api.Endpoints;

public static class MorningBriefTestEndpoints
{
    public static IEndpointRouteBuilder MapMorningBriefTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/admin/morning-briefs/test/{userIndex:int}", async (
            int userIndex, HttpRequest request, IConfiguration configuration, UserDirectory users,
            MorningBriefScheduler scheduler, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var key = configuration["MorningBriefs:TestKey"];
            if (string.IsNullOrEmpty(key) || key.Length < 32) return Results.NotFound();
            var supplied = request.Headers["X-Morning-Brief-Key"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
                return Results.Unauthorized();
            var user = userIndex >= 0 ? users.All.ElementAtOrDefault(userIndex) : null;
            if (user is null) return Results.NotFound();
            var result = await scheduler.RunNowAsync(user, cancellationToken);
            loggerFactory.CreateLogger("MorningBriefTest").LogInformation(
                "Manual morning brief for {UserName}: {Result}", user.Name, result);
            return Results.Ok(new { result = result.ToString() });
        });
        return endpoints;
    }
}
