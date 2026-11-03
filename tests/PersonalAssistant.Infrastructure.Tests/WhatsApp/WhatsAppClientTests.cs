using System.Net;
using System.Text.Json;
using PersonalAssistant.Core.WhatsApp;
using PersonalAssistant.Infrastructure.Tests.Claude;
using PersonalAssistant.Infrastructure.WhatsApp;

namespace PersonalAssistant.Infrastructure.Tests.WhatsApp;

public class WhatsAppClientTests
{
    private const string SuccessResponse = """
        {
          "messaging_product": "whatsapp",
          "contacts": [{ "input": "353800000001", "wa_id": "353800000001" }],
          "messages": [{ "id": "wamid.SENT1" }]
        }
        """;

    private static readonly WhatsAppOptions Options = new()
    {
        AccessToken = "test-access-token",
        PhoneNumberId = "123456",
        GraphApiVersion = "v25.0"
    };

    [Fact]
    public async Task SendTextAsync_PostsToPhoneNumberMessagesEndpoint()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.SendTextAsync("353800000001", "Olá!");

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(new Uri("https://graph.facebook.com/v25.0/123456/messages"), handler.LastRequestUri);
    }

    [Fact]
    public async Task SendTextAsync_SendsBearerToken()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.SendTextAsync("353800000001", "Olá!");

        Assert.Equal("Bearer test-access-token", handler.LastHeaders["Authorization"]);
    }

    [Fact]
    public async Task SendTextAsync_SendsTextMessageBody()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.SendTextAsync("353800000001", "Olá!");

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("353800000001", root.GetProperty("to").GetString());
        Assert.Equal("text", root.GetProperty("type").GetString());
        Assert.Equal("Olá!", root.GetProperty("text").GetProperty("body").GetString());
    }

    [Fact]
    public async Task SendTextAsync_SplitsLongText_IntoSeveralMessages()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);
        var longText = new string('a', 5000);

        await client.SendTextAsync("353800000001", longText);

        var bodies = handler.Requests
            .Select(request => JsonDocument.Parse(request.Body!).RootElement.GetProperty("text").GetProperty("body").GetString())
            .ToList();
        Assert.Equal(2, bodies.Count);
        Assert.Equal(4096, bodies[0]!.Length);
        Assert.Equal(904, bodies[1]!.Length);
    }

    [Fact]
    public async Task SendTextAsync_ThrowsWhatsAppApiException_WhenResponseIsNotSuccessful()
    {
        const string errorResponse = """
            { "error": { "message": "Invalid OAuth access token.", "type": "OAuthException", "code": 190 } }
            """;
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized, errorResponse);

        var exception = await Assert.ThrowsAsync<WhatsAppApiException>(
            () => client.SendTextAsync("353800000001", "Olá!"));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.DoesNotContain("test-access-token", exception.Message);
    }

    private static (WhatsAppClient Client, FakeHttpMessageHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var client = new WhatsAppClient(new HttpClient(handler), Options);

        return (client, handler);
    }
}
