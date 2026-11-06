using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Infrastructure.WhatsApp;

/// <summary>
/// Sends text messages through the WhatsApp Cloud API.
/// Texts longer than the WhatsApp limit are split into several messages.
/// </summary>
public sealed class WhatsAppClient(HttpClient httpClient, WhatsAppOptions options, ILogger<WhatsAppClient> logger) : IMessageSender
{
    public const int MaxTextLength = 4096;

    public async Task SendTextAsync(string recipient, string text, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in SplitText(text))
        {
            await SendChunkAsync(recipient, chunk, cancellationToken);
        }
    }

    public Task SendTemplateAsync(string recipient, string templateName, string languageCode,
        IReadOnlyList<string>? bodyParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        if (bodyParameters?.Any(string.IsNullOrWhiteSpace) == true)
            throw new ArgumentException("Template parameters cannot be empty.", nameof(bodyParameters));

        var template = new Dictionary<string, object>
        {
            ["name"] = templateName,
            ["language"] = new { code = languageCode }
        };
        if (bodyParameters is { Count: > 0 })
        {
            template["components"] = new[]
            {
                new { type = "body", parameters = bodyParameters.Select(value => new { type = "text", text = value }).ToArray() }
            };
        }
        return SendAsync(new
        {
            messaging_product = "whatsapp",
            to = recipient,
            type = "template",
            template
        }, cancellationToken);
    }

    private Task SendChunkAsync(string recipient, string text, CancellationToken cancellationToken) =>
        SendAsync(new SendTextRequest(recipient, new TextContent(text)), cancellationToken);

    private async Task SendAsync<T>(T payload, CancellationToken cancellationToken)
    {
        var uri = new Uri(
            new Uri(options.GraphApiBaseUrl),
            $"{options.GraphApiVersion}/{options.PhoneNumberId}/messages");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogApiError((int)response.StatusCode, errorBody);

            throw new WhatsAppApiException(
                response.StatusCode,
                $"WhatsApp API returned status {(int)response.StatusCode}.",
                errorBody);
        }
    }

    private void LogApiError(int statusCode, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object)
            {
                string? Field(JsonElement element, string name)
                {
                    if (!element.TryGetProperty(name, out var value)) return null;
                    var text = value.ToString();
                    return string.IsNullOrEmpty(options.AccessToken)
                        ? text : text.Replace(options.AccessToken, "[redacted]", StringComparison.Ordinal);
                }

                var details = error.TryGetProperty("error_data", out var data) && data.ValueKind == JsonValueKind.Object
                    ? Field(data, "details") : null;
                logger.LogWarning("WhatsApp API rejected message: HTTP {StatusCode}, code {MetaCode}, subcode {MetaSubcode}, message {MetaMessage}, details {MetaDetails}, trace {MetaTraceId}",
                    statusCode, Field(error, "code"), Field(error, "error_subcode"),
                    Field(error, "message"), details, Field(error, "fbtrace_id"));
                return;
            }
        }
        catch (JsonException)
        {
            // Gateways can return non-JSON error bodies.
        }

        logger.LogWarning("WhatsApp API rejected message: HTTP {StatusCode}; no structured Meta error available", statusCode);
    }

    private static IEnumerable<string> SplitText(string text)
    {
        var start = 0;

        while (start < text.Length)
        {
            var length = Math.Min(MaxTextLength, text.Length - start);

            // Do not cut an emoji (surrogate pair) in half.
            if (start + length < text.Length && char.IsHighSurrogate(text[start + length - 1]))
            {
                length--;
            }

            yield return text.Substring(start, length);

            start += length;
        }
    }
}

internal sealed record SendTextRequest(
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("text")] TextContent Text)
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct => "whatsapp";

    [JsonPropertyName("recipient_type")]
    public string RecipientType => "individual";

    [JsonPropertyName("type")]
    public string Type => "text";
}

internal sealed record TextContent(
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("preview_url")] bool PreviewUrl = false);
