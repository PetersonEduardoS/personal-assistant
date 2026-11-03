using System.Net;
using System.Text.Json;
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

        var reply = await client.GetReplyAsync("Oi");

        Assert.Equal("Olá, Eduardo!", reply);
    }

    [Fact]
    public async Task GetReplyAsync_SendsPostToMessagesEndpoint()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.GetReplyAsync("Oi");

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(new Uri("https://api.anthropic.com/v1/messages"), handler.LastRequestUri);
    }

    [Fact]
    public async Task GetReplyAsync_SendsApiKeyAndVersionHeaders()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.GetReplyAsync("Oi");

        Assert.Equal("test-api-key", handler.LastHeaders["x-api-key"]);
        Assert.Equal("2023-06-01", handler.LastHeaders["anthropic-version"]);
    }

    [Fact]
    public async Task GetReplyAsync_SendsModelSystemPromptAndUserMessage()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessResponse);

        await client.GetReplyAsync("Oi, tudo bem?");

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal(300, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("Test system prompt", root.GetProperty("system").GetString());

        var message = Assert.Single(root.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.Equal("Oi, tudo bem?", message.GetProperty("content").GetString());
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

        var reply = await client.GetReplyAsync("Oi");

        Assert.Equal("Primeira parte. Segunda parte.", reply);
    }

    [Fact]
    public async Task GetReplyAsync_ThrowsClaudeApiException_WhenResponseIsNotSuccessful()
    {
        const string errorResponse = """
            { "type": "error", "error": { "type": "authentication_error", "message": "invalid x-api-key" } }
            """;
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized, errorResponse);

        var exception = await Assert.ThrowsAsync<ClaudeApiException>(() => client.GetReplyAsync("Oi"));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.DoesNotContain("test-api-key", exception.Message);
    }

    [Fact]
    public async Task GetReplyAsync_ThrowsClaudeApiException_WhenResponseHasNoText()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, """{ "content": [] }""");

        await Assert.ThrowsAsync<ClaudeApiException>(() => client.GetReplyAsync("Oi"));
    }

    private static (ClaudeClient Client, FakeHttpMessageHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var client = new ClaudeClient(new HttpClient(handler), Options);

        return (client, handler);
    }
}
