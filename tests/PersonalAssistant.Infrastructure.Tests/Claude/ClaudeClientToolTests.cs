using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Infrastructure.Claude;

namespace PersonalAssistant.Infrastructure.Tests.Claude;

public class ClaudeClientToolTests
{
    private static readonly ClaudeOptions Options = new()
    {
        ApiKey = "test-api-key",
        Model = "test-model",
        SystemPrompt = "Test system prompt"
    };

    private const string FinalAnswer = """
        { "stop_reason": "end_turn", "content": [{ "type": "text", "text": "Anotado!" }] }
        """;

    private static string ToolUse(string id, string name, string input) => $$"""
        {
          "stop_reason": "tool_use",
          "content": [
            { "type": "text", "text": "Vou anotar." },
            { "type": "tool_use", "id": "{{id}}", "name": "{{name}}", "input": {{input}} }
          ]
        }
        """;

    [Fact]
    public async Task GetReplyAsync_SendsTheToolDefinitions()
    {
        var handler = new SequenceHttpMessageHandler(FinalAnswer);
        var tool = new EchoTool();

        await CreateClient(handler).GetReplyAsync(Request(tool));

        var sent = Assert.Single(Body(handler, 0).GetProperty("tools").EnumerateArray());
        Assert.Equal("echo", sent.GetProperty("name").GetString());
        Assert.Equal("Repeats a value.", sent.GetProperty("description").GetString());
        Assert.Equal("object", sent.GetProperty("input_schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetReplyAsync_RunsTheTool_AndSendsTheResultBack()
    {
        var handler = new SequenceHttpMessageHandler(ToolUse("toolu_1", "echo", """{ "value": "abc" }"""), FinalAnswer);
        var tool = new EchoTool();

        var reply = await CreateClient(handler).GetReplyAsync(Request(tool));

        Assert.Equal("Anotado!", reply);
        Assert.Equal(new[] { "abc" }, tool.ReceivedValues);

        var messages = Body(handler, 1).GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(3, messages.Count);
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("tool_use", messages[1].GetProperty("content")[1].GetProperty("type").GetString());

        var result = Assert.Single(messages[2].GetProperty("content").EnumerateArray());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        Assert.Equal("tool_result", result.GetProperty("type").GetString());
        Assert.Equal("toolu_1", result.GetProperty("tool_use_id").GetString());
        Assert.Equal("echo: abc", result.GetProperty("content").GetString());
        Assert.False(result.TryGetProperty("is_error", out _));
    }

    [Fact]
    public async Task GetReplyAsync_SendsToolErrorsBack_SoTheAssistantCanExplainThem()
    {
        var handler = new SequenceHttpMessageHandler(ToolUse("toolu_1", "echo", """{ "value": "" }"""), FinalAnswer);

        var reply = await CreateClient(handler).GetReplyAsync(Request(new EchoTool()));

        Assert.Equal("Anotado!", reply);
        var result = LastToolResult(handler);
        Assert.True(result.GetProperty("is_error").GetBoolean());
        Assert.Equal("O valor está vazio.", result.GetProperty("content").GetString());
    }

    [Fact]
    public async Task GetReplyAsync_HidesTheDetailsOfUnexpectedToolFailures()
    {
        var handler = new SequenceHttpMessageHandler(ToolUse("toolu_1", "echo", """{ "value": "crash" }"""), FinalAnswer);

        await CreateClient(handler).GetReplyAsync(Request(new EchoTool()));

        var result = LastToolResult(handler);
        Assert.True(result.GetProperty("is_error").GetBoolean());
        Assert.DoesNotContain("secret", result.GetProperty("content").GetString());
    }

    [Fact]
    public async Task GetReplyAsync_ReportsUnknownTools_AsErrors()
    {
        var handler = new SequenceHttpMessageHandler(ToolUse("toolu_1", "delete_everything", "{}"), FinalAnswer);

        await CreateClient(handler).GetReplyAsync(Request(new EchoTool()));

        var result = LastToolResult(handler);
        Assert.True(result.GetProperty("is_error").GetBoolean());
        Assert.Contains("delete_everything", result.GetProperty("content").GetString());
    }

    [Fact]
    public async Task GetReplyAsync_StopsAfterTheMaximumNumberOfToolRounds()
    {
        var handler = new SequenceHttpMessageHandler(ToolUse("toolu_1", "echo", """{ "value": "again" }"""));

        await Assert.ThrowsAsync<ClaudeApiException>(() => CreateClient(handler).GetReplyAsync(Request(new EchoTool())));

        Assert.Equal(ClaudeClient.MaxToolRounds + 1, handler.RequestBodies.Count);
    }

    [Fact]
    public async Task GetReplyAsync_ContinuesAfterAPausedTurn()
    {
        const string paused = """
            {
              "stop_reason": "pause_turn",
              "content": [{ "type": "server_tool_use", "id": "srvtoolu_1", "name": "web_search", "input": { "query": "tempo" } }]
            }
            """;
        var handler = new SequenceHttpMessageHandler(paused, FinalAnswer);

        var reply = await CreateClient(handler).GetReplyAsync(Request());

        Assert.Equal("Anotado!", reply);
        var messages = Body(handler, 1).GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal("assistant", messages[^1].GetProperty("role").GetString());
        Assert.Equal("server_tool_use", messages[^1].GetProperty("content")[0].GetProperty("type").GetString());
    }

    private static ClaudeClient CreateClient(SequenceHttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options,
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 10, 23, 55, 0, TimeSpan.Zero)),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"));

    private static AssistantRequest Request(params IAssistantTool[] tools) =>
        new("Eduardo", [new ConversationTurn(ConversationRole.User, "Oi")], Tools: tools);

    private static JsonElement Body(SequenceHttpMessageHandler handler, int index) =>
        JsonDocument.Parse(handler.RequestBodies[index]).RootElement;

    private static JsonElement LastToolResult(SequenceHttpMessageHandler handler) =>
        Body(handler, handler.RequestBodies.Count - 1)
            .GetProperty("messages").EnumerateArray().Last()
            .GetProperty("content")[0];

    private sealed class EchoTool : IAssistantTool
    {
        public List<string> ReceivedValues { get; } = [];

        public string Name => "echo";

        public string Description => "Repeats a value.";

        public string InputSchema => """
            { "type": "object", "properties": { "value": { "type": "string" } }, "required": ["value"] }
            """;

        public Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
        {
            var value = input.GetProperty("value").GetString() ?? string.Empty;
            ReceivedValues.Add(value);

            return value switch
            {
                "" => throw new AssistantToolException("O valor está vazio."),
                "crash" => throw new InvalidOperationException("secret connection string"),
                _ => Task.FromResult($"echo: {value}")
            };
        }
    }
}
