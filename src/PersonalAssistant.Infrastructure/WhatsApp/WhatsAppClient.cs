using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Infrastructure.WhatsApp;

/// <summary>
/// Sends text messages through the WhatsApp Cloud API.
/// Texts longer than the WhatsApp limit are split into several messages.
/// </summary>
public sealed class WhatsAppClient(HttpClient httpClient, WhatsAppOptions options) : IMessageSender
{
    public const int MaxTextLength = 4096;

    public async Task SendTextAsync(string recipient, string text, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in SplitText(text))
        {
            await SendChunkAsync(recipient, chunk, cancellationToken);
        }
    }

    private async Task SendChunkAsync(string recipient, string text, CancellationToken cancellationToken)
    {
        var uri = new Uri(
            new Uri(options.GraphApiBaseUrl),
            $"{options.GraphApiVersion}/{options.PhoneNumberId}/messages");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Content = JsonContent.Create(new SendTextRequest(recipient, new TextContent(text)));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new WhatsAppApiException(
                response.StatusCode,
                $"WhatsApp API returned status {(int)response.StatusCode}.",
                errorBody);
        }
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
