using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public async Task SendTemplateAsync_SendsTemplateNameAndLanguage()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.SendTemplateAsync("353800000001", "morning_brief_ready", "pt_BR");

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("353800000001", root.GetProperty("to").GetString());
        Assert.Equal("template", root.GetProperty("type").GetString());
        var template = root.GetProperty("template");
        Assert.Equal("morning_brief_ready", template.GetProperty("name").GetString());
        Assert.Equal("pt_BR", template.GetProperty("language").GetProperty("code").GetString());
        Assert.False(template.TryGetProperty("components", out _));
        Assert.Equal("Bearer test-access-token", handler.LastHeaders["Authorization"]);
    }

    [Fact]
    public async Task SendTemplateAsync_SendsNameAndDateAsOrderedBodyTextParameters()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);
        await client.SendTemplateAsync("353800000001", "morning_brief_ready", "pt_BR",
            ["Eduardo", "sexta, 6 de novembro"]);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var components = body.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal(1, components.GetArrayLength());
        Assert.Equal("body", components[0].GetProperty("type").GetString());
        var parameters = components[0].GetProperty("parameters");
        Assert.Equal(2, parameters.GetArrayLength());
        Assert.Equal("text", parameters[0].GetProperty("type").GetString());
        Assert.Equal("Eduardo", parameters[0].GetProperty("text").GetString());
        Assert.Equal("text", parameters[1].GetProperty("type").GetString());
        Assert.Equal("sexta, 6 de novembro", parameters[1].GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendTemplateAsync_RejectsEmptyParametersBeforeCallingMeta()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.SendTemplateAsync("353800000001", "morning_brief_ready", "pt_BR", [" "]));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":132001,\"message\":\"Template missing\",\"error_data\":{\"details\":\"Check language\"}}}")]
    [InlineData("Bad Gateway")]
    [InlineData("[]")]
    public async Task SendTemplateAsync_PreservesApiFailure(string response)
    {
        var (client, _) = CreateClient(HttpStatusCode.BadRequest, response);

        var exception = await Assert.ThrowsAsync<WhatsAppApiException>(() =>
            client.SendTemplateAsync("353800000001", "morning_brief_ready", "pt_BR"));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(response, exception.ResponseBody);
    }

    [Fact]
    public async Task SendTemplateAsync_LogsMetaReasonAndRedactsToken()
    {
        const string response = """
            { "error": { "code": 132001, "error_subcode": 42,
              "message": "Template missing test-access-token",
              "error_data": { "details": "Check pt_BR test-access-token" }, "fbtrace_id": "trace-123" } }
            """;
        var logger = new RecordingLogger();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, response);
        var client = new WhatsAppClient(new HttpClient(handler), Options, logger);

        await Assert.ThrowsAsync<WhatsAppApiException>(() =>
            client.SendTemplateAsync("353800000001", "morning_brief_ready", "pt_BR"));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("132001", entry);
        Assert.Contains("42", entry);
        Assert.Contains("Template missing", entry);
        Assert.Contains("Check pt_BR", entry);
        Assert.Contains("trace-123", entry);
        Assert.Contains("[redacted]", entry);
        Assert.DoesNotContain(Options.AccessToken, entry);
        Assert.DoesNotContain("353800000001", entry);
    }

    private sealed class RecordingLogger : ILogger<WhatsAppClient>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception));
    }

    private static (WhatsAppClient Client, FakeHttpMessageHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var client = new WhatsAppClient(new HttpClient(handler), Options, NullLogger<WhatsAppClient>.Instance);

        return (client, handler);
    }
}
