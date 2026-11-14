namespace PersonalAssistant.Core.Emails;

/// <summary>
/// A whole email, with its body as plain text.
/// </summary>
public sealed record EmailMessage(
    string Id,
    string From,
    string Subject,
    DateTimeOffset ReceivedAt,
    string Body);
