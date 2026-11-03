using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Infrastructure.Claude;

/// <summary>
/// Calls the Claude Messages API to get a reply for a conversation.
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
            request.Conversation.Select(ToRequestMessage).ToList()));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new ClaudeApiException(
                response.StatusCode,
                $"Claude API returned status {(int)response.StatusCode}.",
                errorBody);
        }

        var body = await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken);

        var text = string.Concat(
            (body?.Content ?? [])
                .Where(block => block.Type == TextBlockType)
                .Select(block => block.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ClaudeApiException(response.StatusCode, "Claude API returned no text.");
        }

        return text;
    }

    private string BuildSystemPrompt(AssistantRequest request)
    {
        var systemPrompt = new StringBuilder(options.SystemPrompt);
        systemPrompt.Append($"\n\nVocê está conversando com {request.UserName}.");

        if (!string.IsNullOrWhiteSpace(request.UserContext))
        {
            systemPrompt.Append($"\n\nContexto sobre {request.UserName}:\n{request.UserContext}");
        }

        return systemPrompt.ToString();
    }

    private static RequestMessage ToRequestMessage(ConversationTurn turn) =>
        new(turn.Role == ConversationRole.Assistant ? "assistant" : "user", turn.Content);
}

internal sealed record MessagesRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string SystemPrompt,
    [property: JsonPropertyName("messages")] IReadOnlyList<RequestMessage> Messages);

internal sealed record RequestMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed class MessagesResponse
{
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
