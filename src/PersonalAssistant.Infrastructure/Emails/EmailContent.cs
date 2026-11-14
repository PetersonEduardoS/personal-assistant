using System.Net;
using System.Text.RegularExpressions;

namespace PersonalAssistant.Infrastructure.Emails;

/// <summary>
/// Turns email content into short plain text for the assistant.
/// </summary>
public static partial class EmailContent
{
    public static string ToPlainText(string? textBody, string? htmlBody)
    {
        if (!string.IsNullOrWhiteSpace(textBody))
        {
            return Normalize(textBody);
        }

        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            return string.Empty;
        }

        var text = HiddenBlocks().Replace(htmlBody, string.Empty);
        text = LineBreaks().Replace(text, "\n");
        text = BlockEnds().Replace(text, "\n");
        text = Tags().Replace(text, string.Empty);

        return Normalize(WebUtility.HtmlDecode(text));
    }

    public static string Snippet(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var snippet = AnyWhitespace().Replace(text.Replace('\u00A0', ' '), " ").Trim();

        return snippet.Length > maxLength ? snippet[..maxLength].TrimEnd() + "…" : snippet;
    }

    public static string FormatSender(string? name, string? address)
    {
        var cleanName = name?.Trim();
        var cleanAddress = address?.Trim();

        if (!string.IsNullOrEmpty(cleanName) && !string.IsNullOrEmpty(cleanAddress))
        {
            return $"{cleanName} <{cleanAddress}>";
        }

        return !string.IsNullOrEmpty(cleanAddress) ? cleanAddress
            : !string.IsNullOrEmpty(cleanName) ? cleanName
            : "(remetente desconhecido)";
    }

    private static string Normalize(string text)
    {
        var lines = text
            .Replace('\u00A0', ' ')
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => SpacesAndTabs().Replace(line, " ").Trim());

        return ManyBlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    [GeneratedRegex(@"<(script|style|head)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HiddenBlocks();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"</(p|div|tr|li|h[1-6]|table)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnds();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpacesAndTabs();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyBlankLines();

    [GeneratedRegex(@"\s+")]
    private static partial Regex AnyWhitespace();
}
