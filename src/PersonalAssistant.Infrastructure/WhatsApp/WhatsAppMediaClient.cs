using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PersonalAssistant.Core.Media;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Infrastructure.WhatsApp;

/// <summary>
/// Downloads WhatsApp media in two steps: asks Meta for the file's temporary URL, then downloads it.
/// Both requests carry the access token. The temporary URL works like a password for a few minutes,
/// so it is never logged (the HttpClient for this class has its request logging removed).
/// </summary>
public sealed class WhatsAppMediaClient(
    HttpClient httpClient,
    WhatsAppOptions options,
    ILogger<WhatsAppMediaClient> logger) : IMediaDownloader
{
    private const int BufferSize = 81920;

    public async Task<DownloadedMedia> DownloadAsync(
        string mediaId,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaId);

        var info = await LookUpAsync(mediaId, cancellationToken);

        if (info.FileSize > maxBytes)
        {
            throw new MediaTooLargeException(maxBytes);
        }

        var content = await DownloadContentAsync(info.Url!, maxBytes, cancellationToken);

        return new DownloadedMedia(content, info.MimeType ?? string.Empty);
    }

    private async Task<MediaInfo> LookUpAsync(string mediaId, CancellationToken cancellationToken)
    {
        var uri = new Uri(new Uri(options.GraphApiBaseUrl), $"{options.GraphApiVersion}/{Uri.EscapeDataString(mediaId)}");

        using var request = CreateRequest(uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("WhatsApp media lookup failed: HTTP {StatusCode}", (int)response.StatusCode);
            throw new MediaUnavailableException($"The media lookup returned status {(int)response.StatusCode}.");
        }

        MediaInfo? info;

        try
        {
            info = await JsonSerializer.DeserializeAsync<MediaInfo>(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new MediaUnavailableException("The media lookup was not valid JSON.", exception);
        }

        // Only an HTTPS address is followed, so the token is never sent over plain HTTP.
        if (info is null
            || !Uri.TryCreate(info.Url, UriKind.Absolute, out var fileUri)
            || fileUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new MediaUnavailableException("The media lookup did not return a usable HTTPS address.");
        }

        return info;
    }

    private async Task<byte[]> DownloadContentAsync(string url, long maxBytes, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(new Uri(url));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("WhatsApp media download failed: HTTP {StatusCode}", (int)response.StatusCode);
            throw new MediaUnavailableException($"The media download returned status {(int)response.StatusCode}.");
        }

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw new MediaTooLargeException(maxBytes);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var content = new MemoryStream();
        var buffer = new byte[BufferSize];
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (content.Length + read > maxBytes)
            {
                throw new MediaTooLargeException(maxBytes);
            }

            content.Write(buffer, 0, read);
        }

        return content.ToArray();
    }

    private HttpRequestMessage CreateRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);

        return request;
    }

    private sealed class MediaInfo
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("file_size")]
        public long? FileSize { get; set; }
    }
}
