namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Only senders on the configured list get an answer.
/// Numbers are compared by their digits, so "+353 80..." and "35380..." match.
/// </summary>
public sealed class AllowedSenderPolicy
{
    private readonly HashSet<string> _allowedSenders;

    public AllowedSenderPolicy(IEnumerable<string> allowedSenders)
    {
        _allowedSenders = allowedSenders
            .Select(Normalize)
            .Where(number => number.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool IsAllowed(string? sender) =>
        !string.IsNullOrEmpty(sender) && _allowedSenders.Contains(Normalize(sender));

    private static string Normalize(string number) =>
        new(number.Where(char.IsAsciiDigit).ToArray());
}
