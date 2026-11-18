using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalAssistant.Core.WhatsApp;

/// <summary>
/// Turns the webhook JSON sent by Meta into messages: text, photos, documents, and the types the assistant
/// cannot read yet (so it can say so). Status updates and reactions are ignored.
/// </summary>
public sealed class WebhookPayloadParser
{
    private const string WhatsAppBusinessAccount = "whatsapp_business_account";
    private const string TextMessageType = "text";
    private const string ImageMessageType = "image";
    private const string DocumentMessageType = "document";

    /// <summary>Types that get a "cannot understand this yet" reply instead of silence.</summary>
    private static readonly HashSet<string> UnsupportedTypes = ["audio", "video", "sticker", "location", "contacts"];

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

        if (string.IsNullOrEmpty(id)
            || string.IsNullOrEmpty(from)
            || !long.TryParse(message.Timestamp, out var unixSeconds))
        {
            return false;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        switch (message.Type)
        {
            case TextMessageType when !string.IsNullOrEmpty(message.Text?.Body):
                incomingMessage = new IncomingMessage(id, from, message.Text!.Body!, timestamp);
                return true;

            case ImageMessageType when TryGetAttachment(message.Image, out var image):
                incomingMessage = new IncomingMessage(
                    id, from, message.Image!.Caption ?? string.Empty, timestamp, IncomingMessageKind.Image, image);
                return true;

            case DocumentMessageType when TryGetAttachment(message.Document, out var document):
                incomingMessage = new IncomingMessage(
                    id, from, message.Document!.Caption ?? string.Empty, timestamp, IncomingMessageKind.Document, document);
                return true;

            case { } type when UnsupportedTypes.Contains(type):
                incomingMessage = new IncomingMessage(
                    id, from, string.Empty, timestamp, IncomingMessageKind.Unsupported, UnsupportedType: type);
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetAttachment(WebhookMediaDto? media, [NotNullWhen(true)] out MessageAttachment? attachment)
    {
        attachment = string.IsNullOrEmpty(media?.Id)
            ? null
            : new MessageAttachment(media.Id, media.MimeType ?? string.Empty, media.FileName);

        return attachment is not null;
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

    [JsonPropertyName("image")]
    public WebhookMediaDto? Image { get; set; }

    [JsonPropertyName("document")]
    public WebhookMediaDto? Document { get; set; }
}

internal sealed class WebhookMediaDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }
}

internal sealed class WebhookTextDto
{
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}
