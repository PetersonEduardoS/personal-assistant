namespace PersonalAssistant.Core.Media;

/// <summary>A downloaded photo or file.</summary>
public sealed record DownloadedMedia(byte[] Content, string MimeType);

/// <summary>
/// Downloads a photo or file sent on WhatsApp. Throws <see cref="MediaTooLargeException"/> when it is bigger
/// than <c>maxBytes</c> and <see cref="MediaUnavailableException"/> when it cannot be downloaded.
/// </summary>
public interface IMediaDownloader
{
    Task<DownloadedMedia> DownloadAsync(string mediaId, long maxBytes, CancellationToken cancellationToken = default);
}

public sealed class MediaTooLargeException(long maxBytes)
    : Exception($"The file is larger than {maxBytes} bytes.")
{
    public long MaxBytes { get; } = maxBytes;
}

public sealed class MediaUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
