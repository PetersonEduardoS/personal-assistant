using Microsoft.AspNetCore.Mvc;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Api.Endpoints;

public static class WhatsAppWebhookEndpoints
{
    public const string Route = "/webhooks/whatsapp";

    public static IEndpointRouteBuilder MapWhatsAppWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, VerifyWebhook);

        return endpoints;
    }

    private static IResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        WebhookVerifier verifier)
    {
        var response = verifier.Verify(mode, verifyToken, challenge);

        return response is null
            ? Results.StatusCode(StatusCodes.Status403Forbidden)
            : Results.Text(response, "text/plain");
    }
}
