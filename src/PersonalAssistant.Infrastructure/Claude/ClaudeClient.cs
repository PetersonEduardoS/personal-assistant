using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Infrastructure.Claude;

/// <summary>
/// Calls the Claude Messages API to get a reply for a conversation.
/// When web search is enabled, Claude can search the web before answering.
/// </summary>
public sealed class ClaudeClient(HttpClient httpClient, ClaudeOptions options) : IAssistantClient
{
    private const string MessagesPath = "v1/messages";
    private const string TextBlockType = "text";

    public async Task<string> GetReplyAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.BaseUrl), MessagesPath));
        httpRequest.Headers.Add("x-api-key", options.ApiKey);
        httpRequest.Headers.Add("anthropic-version", options.ApiVersion);
        httpRequest.Content = JsonContent.Create(new MessagesRequest(
            options.Model,
            options.MaxTokens,
            BuildSystemPrompt(request),
            request.Conversation.Select(ToRequestMessage).ToList(),
            BuildTools()));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiError = TryReadApiError(errorBody);
            var message = apiError is null
                ? $"Claude API returned status {(int)response.StatusCode}."
                : $"Claude API returned status {(int)response.StatusCode}: {apiError}";

            throw new ClaudeApiException(response.StatusCode, message, errorBody);
        }

        var body = await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken);

        // Search blocks (server_tool_use, web_search_tool_result) are skipped; only the text goes to WhatsApp.
        var text = string.Concat(
            (body?.Content ?? [])
                .Where(block => block.Type == TextBlockType)
                .Select(block => block.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            var blockTypes = string.Join(", ", (body?.Content ?? []).Select(block => block.Type));
            throw new ClaudeApiException(response.StatusCode,
                $"Claude API returned no text (stop_reason: {body?.StopReason ?? "unknown"}; max_tokens: {options.MaxTokens}; blocks: {blockTypes}).");
        }

        return text;
    }

    private string BuildSystemPrompt(AssistantRequest request)
    {
        var systemPrompt = new StringBuilder(options.SystemPrompt);
        systemPrompt.Append($"\n\nVocê está conversando com {request.UserName}.");

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

    private IReadOnlyList<WebSearchTool>? BuildTools()
    {
        var webSearch = options.WebSearch;

        if (!webSearch.Enabled)
        {
            return null;
        }

        var hasLocation = !string.IsNullOrWhiteSpace(webSearch.City)
            || !string.IsNullOrWhiteSpace(webSearch.Region)
            || !string.IsNullOrWhiteSpace(webSearch.Country)
            || !string.IsNullOrWhiteSpace(webSearch.Timezone);

        var location = hasLocation
            ? new UserLocation(webSearch.City, webSearch.Region, webSearch.Country, webSearch.Timezone)
            : null;

        return [new WebSearchTool(webSearch.MaxUses, location)];
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
    IReadOnlyList<WebSearchTool>? Tools);

internal sealed record RequestMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

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

internal sealed class MessagesResponse
{
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("content")]
    public List<ContentBlock>? Content { get; set; }
}

internal sealed class ContentBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
