namespace PersonalAssistant.Core.Emails;

/// <summary>
/// Which emails to list: received since <paramref name="Since"/>, optionally from a sender or with a text.
/// </summary>
public sealed record EmailQuery(DateTimeOffset Since, string? From, string? Text, int MaxCount);
