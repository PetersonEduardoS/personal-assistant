using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalAssistant.Core.WhatsApp;

/// <summary>
/// Turns the webhook JSON sent by Meta into simple text messages.
/// Status updates and non-text messages are ignored.
/// </summary>
public sealed class WebhookPayloadParser
{
    private const string WhatsAppBusinessAccount = "whatsapp_business_account";
    private const string TextMessageType = "text";

    public IReadOnlyList<IncomingMessage> Parse(string json)
    {
        var payload = TryDeserialize(json);

        if (payload?.Object != WhatsAppBusinessAccount || payload.Entry is null)
        {
            return [];
        }

        var messages = new List<IncomingMessage>();

        foreach (var entry in payload.Entry)
        {
            if (entry.Changes is null)
            {
                continue;
            }

            foreach (var change in entry.Changes)
            {
                if (change.Value?.Messages is null)
                {
                    continue;
                }

                foreach (var message in change.Value.Messages)
                {
                    if (TryMap(message, out var incomingMessage))
                    {
                        messages.Add(incomingMessage);
                    }
                }
            }
        }

        return messages;
    }

    private static WebhookPayloadDto? TryDeserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WebhookPayloadDto>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryMap(WebhookMessageDto message, [NotNullWhen(true)] out IncomingMessage? incomingMessage)
    {
        incomingMessage = null;

        var id = message.Id;
        var from = message.From;
        var body = message.Text?.Body;

        if (message.Type != TextMessageType
            || string.IsNullOrEmpty(id)
            || string.IsNullOrEmpty(from)
            || string.IsNullOrEmpty(body)
            || !long.TryParse(message.Timestamp, out var unixSeconds))
        {
            return false;
        }

        incomingMessage = new IncomingMessage(id, from, body, DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        return true;
    }
}

internal sealed class WebhookPayloadDto
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("entry")]
    public List<WebhookEntryDto>? Entry { get; set; }
}

internal sealed class WebhookEntryDto
{
    [JsonPropertyName("changes")]
    public List<WebhookChangeDto>? Changes { get; set; }
}

internal sealed class WebhookChangeDto
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("value")]
    public WebhookValueDto? Value { get; set; }
}

internal sealed class WebhookValueDto
{
    [JsonPropertyName("messages")]
    public List<WebhookMessageDto>? Messages { get; set; }
}

internal sealed class WebhookMessageDto
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public WebhookTextDto? Text { get; set; }
}

internal sealed class WebhookTextDto
{
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}
