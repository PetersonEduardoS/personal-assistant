using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.MorningBriefs;

namespace PersonalAssistant.Infrastructure.Claude;

/// <summary>
/// Calls the Claude Messages API to get a reply for a conversation.
/// When web search is enabled, Claude can search the web before answering.
/// When the request has tools, Claude can ask to use them: the app runs each tool, sends the result back
/// and asks again, until Claude answers with text.
/// The current date and time in Dublin go in the system prompt, so words like "amanhã" or "sexta" make sense.
/// </summary>
public sealed class ClaudeClient(
    HttpClient httpClient,
    ClaudeOptions options,
    TimeProvider clock,
    TimeZoneInfo timeZone,
    ILogger<ClaudeClient>? logger = null) : IAssistantClient
{
    /// <summary>The most tool rounds in one reply, so a confused loop cannot run forever or spend credits.</summary>
    public const int MaxToolRounds = 5;

    private const string MessagesPath = "v1/messages";
    private const string TextBlockType = "text";
    private const string ToolUseBlockType = "tool_use";
    private const string ToolUseStopReason = "tool_use";
    private const string PauseTurnStopReason = "pause_turn";
    private const string UnexpectedToolFailure = "Erro inesperado ao executar a ferramenta. Tente de novo mais tarde.";

    private readonly ILogger<ClaudeClient> _logger = logger ?? NullLogger<ClaudeClient>.Instance;

    public async Task<string> GetReplyAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        var tools = request.Tools ?? [];
        var systemPrompt = BuildSystemPrompt(request);
        var toolDefinitions = BuildTools(tools);
        var messages = request.Conversation.Select(ToRequestMessage).ToList();

        for (var round = 0; ; round++)
        {
            var response = await SendAsync(systemPrompt, messages, toolDefinitions, cancellationToken);

            if (response.StopReason is not (ToolUseStopReason or PauseTurnStopReason))
            {
                return ReadText(response);
            }

            if (round >= MaxToolRounds)
            {
                throw new ClaudeApiException(
                    System.Net.HttpStatusCode.OK,
                    $"Claude API asked for more than {MaxToolRounds} tool rounds.");
            }

            // The assistant turn goes back exactly as it came, including search and tool blocks.
            messages.Add(new RequestMessage("assistant", response.Content));

            if (response.StopReason == ToolUseStopReason)
            {
                var results = new List<ToolResultBlock>();

                foreach (var block in response.Content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var type) && type.GetString() == ToolUseBlockType)
                    {
                        results.Add(await RunToolAsync(block, tools, cancellationToken));
                    }
                }

                messages.Add(new RequestMessage("user", results));
            }
        }
    }

    private async Task<ClaudeResponse> SendAsync(
        string systemPrompt,
        IReadOnlyList<RequestMessage> messages,
        IReadOnlyList<object>? tools,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.BaseUrl), MessagesPath));
        httpRequest.Headers.Add("x-api-key", options.ApiKey);
        httpRequest.Headers.Add("anthropic-version", options.ApiVersion);
        httpRequest.Content = JsonContent.Create(new MessagesRequest(
            options.Model,
            options.MaxTokens,
            systemPrompt,
            messages,
            tools));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var apiError = TryReadApiError(body);
            var message = apiError is null
                ? $"Claude API returned status {(int)response.StatusCode}."
                : $"Claude API returned status {(int)response.StatusCode}: {apiError}";

            throw new ClaudeApiException(response.StatusCode, message, body);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var stopReason = root.TryGetProperty("stop_reason", out var stopElement) && stopElement.ValueKind == JsonValueKind.String
            ? stopElement.GetString()
            : null;

        var content = root.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Array
            ? contentElement.Clone()
            : JsonDocument.Parse("[]").RootElement.Clone();

        return new ClaudeResponse(stopReason, content);
    }

    private string ReadText(ClaudeResponse response)
    {
        // Search blocks (server_tool_use, web_search_tool_result) are skipped; only the text goes to WhatsApp.
        var blocks = response.Content.EnumerateArray().ToList();
        var text = string.Concat(blocks
            .Where(block => block.TryGetProperty("type", out var type) && type.GetString() == TextBlockType)
            .Select(block => block.TryGetProperty("text", out var textElement) ? textElement.GetString() : null));

        if (string.IsNullOrWhiteSpace(text))
        {
            var blockTypes = string.Join(", ", blocks.Select(block =>
                block.TryGetProperty("type", out var type) ? type.GetString() : null));

            throw new ClaudeApiException(System.Net.HttpStatusCode.OK,
                $"Claude API returned no text (stop_reason: {response.StopReason ?? "unknown"}; max_tokens: {options.MaxTokens}; blocks: {blockTypes}).");
        }

        return text;
    }

    private async Task<ToolResultBlock> RunToolAsync(
        JsonElement toolUse,
        IReadOnlyList<IAssistantTool> tools,
        CancellationToken cancellationToken)
    {
        var id = toolUse.GetProperty("id").GetString() ?? string.Empty;
        var name = toolUse.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var input = toolUse.TryGetProperty("input", out var inputElement)
            ? inputElement
            : JsonDocument.Parse("{}").RootElement.Clone();

        var tool = tools.FirstOrDefault(candidate => candidate.Name == name);

        if (tool is null)
        {
            return new ToolResultBlock(id, $"Ferramenta desconhecida: {name}.", IsError: true);
        }

        try
        {
            return new ToolResultBlock(id, await tool.ExecuteAsync(input, cancellationToken));
        }
        catch (AssistantToolException exception)
        {
            return new ToolResultBlock(id, exception.Message, IsError: true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The details may contain private data, so the assistant only learns that the tool failed.
            _logger.LogError(exception, "The assistant tool {ToolName} failed", tool.Name);

            return new ToolResultBlock(id, UnexpectedToolFailure, IsError: true);
        }
    }

    private string BuildSystemPrompt(AssistantRequest request)
    {
        var systemPrompt = new StringBuilder(options.SystemPrompt);
        systemPrompt.Append($"\n\nVocê está conversando com {request.UserName}.");
        systemPrompt.Append($"\n\nAgora em Dublin: {FormatNow()}.");

        if (options.WebSearch.Enabled)
        {
            systemPrompt.Append(
                "\n\nQuando a pergunta depender de informação atual, como tempo, notícias ou preços, " +
                "pesquise na internet antes de responder. Não cole links longos na resposta.");
        }

        if (!string.IsNullOrWhiteSpace(request.UserContext))
        {
            systemPrompt.Append($"\n\nContexto sobre {request.UserName}:\n{request.UserContext}");
        }

        return systemPrompt.ToString();
    }

    /// <summary>
    /// For example "terça-feira, 10 de novembro de 2026, 14:56".
    /// </summary>
    private string FormatNow()
    {
        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone);
        var date = PortugueseDates.FormatLong(DateOnly.FromDateTime(now.DateTime));

        return $"{date}, {now.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    private IReadOnlyList<object>? BuildTools(IReadOnlyList<IAssistantTool> tools)
    {
        var definitions = new List<object>();
        var webSearch = options.WebSearch;

        if (webSearch.Enabled)
        {
            var hasLocation = !string.IsNullOrWhiteSpace(webSearch.City)
                || !string.IsNullOrWhiteSpace(webSearch.Region)
                || !string.IsNullOrWhiteSpace(webSearch.Country)
                || !string.IsNullOrWhiteSpace(webSearch.Timezone);

            var location = hasLocation
                ? new UserLocation(webSearch.City, webSearch.Region, webSearch.Country, webSearch.Timezone)
                : null;

            definitions.Add(new WebSearchTool(webSearch.MaxUses, location));
        }

        foreach (var tool in tools)
        {
            using var schema = JsonDocument.Parse(tool.InputSchema);
            definitions.Add(new CustomTool(tool.Name, tool.Description, schema.RootElement.Clone()));
        }

        return definitions.Count == 0 ? null : definitions;
    }

    /// <summary>
    /// Reads "type: message" from the API error body, so logs show why a request failed.
    /// The error body never contains the API key.
    /// </summary>
    private static string? TryReadApiError(string errorBody)
    {
        try
        {
            using var document = JsonDocument.Parse(errorBody);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var type = error.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            var detail = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;

            return string.IsNullOrWhiteSpace(detail) ? type : $"{type}: {detail}";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RequestMessage ToRequestMessage(ConversationTurn turn) =>
        new(turn.Role == ConversationRole.Assistant ? "assistant" : "user", turn.Content);
}

internal sealed record MessagesRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string SystemPrompt,
    [property: JsonPropertyName("messages")] IReadOnlyList<RequestMessage> Messages,
    [property: JsonPropertyName("tools"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<object>? Tools);

/// <param name="Content">Plain text, a list of tool results, or the content blocks of an earlier answer.</param>
internal sealed record RequestMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] object Content);

internal sealed record CustomTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("input_schema")] JsonElement InputSchema);

internal sealed record ToolResultBlock(
    [property: JsonPropertyName("tool_use_id")] string ToolUseId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("is_error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool IsError = false)
{
    [JsonPropertyName("type")]
    public string Type => "tool_result";
}

internal sealed record ClaudeResponse(string? StopReason, JsonElement Content);

internal sealed record WebSearchTool(
    [property: JsonPropertyName("max_uses")] int MaxUses,
    [property: JsonPropertyName("user_location"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    UserLocation? UserLocation)
{
    [JsonPropertyName("type")]
    public string Type => "web_search_20250305";

    [JsonPropertyName("name")]
    public string Name => "web_search";
}

internal sealed record UserLocation(
    [property: JsonPropertyName("city"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? City,
    [property: JsonPropertyName("region"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Region,
    [property: JsonPropertyName("country"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Country,
    [property: JsonPropertyName("timezone"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Timezone)
{
    [JsonPropertyName("type")]
    public string Type => "approximate";
}
