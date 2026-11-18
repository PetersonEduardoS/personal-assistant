namespace PersonalAssistant.Core.Media;

/// <summary>Which photos and files the assistant reads, and how big they can be.</summary>
public static class MediaLimits
{
    public const long MaxImageBytes = 5 * 1024 * 1024;

    public const long MaxDocumentBytes = 10 * 1024 * 1024;

    public const string PdfType = "application/pdf";

    public static readonly IReadOnlySet<string> ImageTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    /// <summary>The media type without parameters, for example "image/jpeg" from "image/jpeg; charset=x".</summary>
    public static string Normalize(string? mimeType) =>
        (mimeType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
}
