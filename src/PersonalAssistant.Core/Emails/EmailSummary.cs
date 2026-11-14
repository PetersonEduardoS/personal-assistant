namespace PersonalAssistant.Core.Emails;

/// <summary>
/// One email in a list: who sent it, the subject and a short preview.
/// </summary>
public sealed record EmailSummary(
    string Id,
    string From,
    string Subject,
    DateTimeOffset ReceivedAt,
    string Snippet,
    bool IsUnread);
