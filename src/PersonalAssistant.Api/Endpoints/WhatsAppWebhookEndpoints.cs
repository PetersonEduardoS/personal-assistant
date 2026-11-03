using System.Text;
using Microsoft.AspNetCore.Mvc;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Api.Endpoints;

public static class WhatsAppWebhookEndpoints
{
    public const string Route = "/webhooks/whatsapp";

    private const string SignatureHeader = "X-Hub-Signature-256";

    public static IEndpointRouteBuilder MapWhatsAppWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, VerifyWebhook);
        endpoints.MapPost(Route, ReceiveWebhook);

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

    private static async Task<IResult> ReceiveWebhook(
        HttpRequest request,
        WebhookSignatureValidator signatureValidator,
        WebhookPayloadParser payloadParser,
        AllowedSenderPolicy senderPolicy,
        IIncomingMessageQueue messageQueue,
        CancellationToken cancellationToken)
    {
        var payload = await ReadBodyAsync(request, cancellationToken);

        if (!signatureValidator.IsValid(payload, request.Headers[SignatureHeader].ToString()))
        {
            return Results.Unauthorized();
        }

        foreach (var message in payloadParser.Parse(Encoding.UTF8.GetString(payload)))
        {
            if (senderPolicy.IsAllowed(message.From))
            {
                await messageQueue.EnqueueAsync(message, cancellationToken);
            }
        }

        return Results.Ok();
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}
