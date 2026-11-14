using System.Globalization;
using PersonalAssistant.Core.Emails;

namespace PersonalAssistant.Infrastructure.Emails;

/// <summary>
/// Builds a Gmail search (the same syntax as the Gmail search box) for the IMAP X-GM-RAW extension.
/// </summary>
public static class GmailSearch
{
    private const int MaxFilterLength = 100;

    public static string Build(EmailQuery query)
    {
        var parts = new List<string>
        {
            "in:inbox",
            "-category:promotions",
            $"after:{query.Since.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}"
        };

        var from = Clean(query.From);
        if (from.Length > 0)
        {
            parts.Add($"from:({from})");
        }

        var text = Clean(query.Text);
        if (text.Length > 0)
        {
            parts.Add($"\"{text}\"");
        }

        return string.Join(' ', parts);
    }

    /// <summary>Removes quotes, brackets and backslashes, so a filter cannot change the rest of the search.</summary>
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var clean = new string(value.Where(character => character is not ('"' or '(' or ')' or '\\')).ToArray()).Trim();

        return clean.Length > MaxFilterLength ? clean[..MaxFilterLength].Trim() : clean;
    }
}
