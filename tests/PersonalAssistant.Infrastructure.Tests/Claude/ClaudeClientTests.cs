using System.Net;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Infrastructure.Claude;

namespace PersonalAssistant.Infrastructure.Tests.Claude;

public class ClaudeClientTests
{
    private const string SuccessResponse = """
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "content": [{ "type": "text", "text": "Olá, Eduardo!" }],
          "stop_reason": "end_turn",
          "usage": { "input_tokens": 12, "output_tokens": 5 }
        }
        """;

    private static readonly ClaudeOptions Options = new()
    {
        ApiKey = "test-api-key",
        Model = "test-model",
        MaxTokens = 300,
        ApiVersion = "2023-06-01",
        SystemPrompt = "Test system prompt"
    };

    [Fact]
    public async Task GetReplyAsync_ReturnsTextFromResponse()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        var reply = await client.GetReplyAsync(Request("Oi"));

        Assert.Equal("Olá, Eduardo!", reply);
    }

    [Fact]
    public async Task GetReplyAsync_SendsPostToMessagesEndpoint()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.GetReplyAsync(Request("Oi"));

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(new Uri("https://api.anthropic.com/v1/messages"), handler.LastRequestUri);
    }

    [Fact]
    public async Task GetReplyAsync_SendsApiKeyAndVersionHeaders()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.GetReplyAsync(Request("Oi"));

        Assert.Equal("test-api-key", handler.LastHeaders["x-api-key"]);
        Assert.Equal("2023-06-01", handler.LastHeaders["anthropic-version"]);
    }

    [Fact]
    public async Task GetReplyAsync_SendsModelAndMaxTokens()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.GetReplyAsync(Request("Oi"));

        var root = ParseBody(handler);
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal(300, root.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task GetReplyAsync_SendsSystemPrompt_WithUserName()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.GetReplyAsync(Request("Oi"));

        var system = ParseBody(handler).GetProperty("system").GetString();
        Assert.StartsWith("Test system prompt", system);
        Assert.Contains("Eduardo", system);
    }

    [Fact]
    public async Task GetReplyAsync_AddsUserContextToSystemPrompt_WhenProvided()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);
        var request = Request("Oi") with { UserContext = "Trabalha com .NET em Dublin." };

        await client.GetReplyAsync(request);

        var system = ParseBody(handler).GetProperty("system").GetString();
        Assert.Contains("Trabalha com .NET em Dublin.", system);
    }

    [Fact]
    public async Task GetReplyAsync_SendsConversation_WithRoles()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);
        var request = new AssistantRequest(
            "Eduardo",
            [
                new ConversationTurn(ConversationRole.User, "Oi"),
                new ConversationTurn(ConversationRole.Assistant, "Olá!"),
                new ConversationTurn(ConversationRole.User, "Tudo bem?")
            ]);

        await client.GetReplyAsync(request);

        var messages = ParseBody(handler).GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(
            new (string?, string?)[] { ("user", "Oi"), ("assistant", "Olá!"), ("user", "Tudo bem?") },
            messages.Select(message => (
                message.GetProperty("role").GetString(),
                message.GetProperty("content").GetString())));
    }

    [Fact]
    public async Task GetReplyAsync_JoinsTextBlocks_AndIgnoresOtherBlocks()
    {
        const string response = """
            {
              "content": [
                { "type": "text", "text": "Primeira parte. " },
                { "type": "tool_use", "id": "toolu_test", "name": "test", "input": {} },
                { "type": "text", "text": "Segunda parte." }
              ]
            }
            """;
        var (client, _) = CreateClient(HttpStatusCode.OK, response);

        var reply = await client.GetReplyAsync(Request("Oi"));

        Assert.Equal("Primeira parte. Segunda parte.", reply);
    }

    [Fact]
    public async Task GetReplyAsync_ThrowsClaudeApiException_WhenResponseIsNotSuccessful()
    {
        const string errorResponse = """
            { "type": "error", "error": { "type": "authentication_error", "message": "invalid x-api-key" } }
            """;
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized, errorResponse);

        var exception = await Assert.ThrowsAsync<ClaudeApiException>(() => client.GetReplyAsync(Request("Oi")));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.DoesNotContain("test-api-key", exception.Message);
    }

    [Fact]
    public async Task GetReplyAsync_ThrowsClaudeApiException_WhenResponseHasNoText()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, """{ "content": [] }""");

        await Assert.ThrowsAsync<ClaudeApiException>(() => client.GetReplyAsync(Request("Oi")));
    }

    private static AssistantRequest Request(string text) =>
        new("Eduardo", [new ConversationTurn(ConversationRole.User, text)]);

    private static JsonElement ParseBody(FakeHttpMessageHandler handler) =>
        JsonDocument.Parse(handler.LastRequestBody!).RootElement;

    private static (ClaudeClient Client, FakeHttpMessageHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var client = new ClaudeClient(new HttpClient(handler), Options);

        return (client, handler);
    }
}
