using PersonalAssistant.Core.Emails;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Infrastructure.Emails;

/// <summary>
/// Placeholder until the IMAP inbox exists: nobody has email yet, so the email tools stay hidden.
/// </summary>
public sealed class DisabledEmailInbox : IEmailInbox
{
    public bool IsConfigured(UserProfile user) => false;

    public Task<IReadOnlyList<EmailSummary>> ListRecentAsync(
        UserProfile user,
        EmailQuery query,
        CancellationToken cancellationToken = default) =>
        throw new EmailUnavailableException("Email is not available yet.");

    public Task<EmailMessage?> ReadAsync(UserProfile user, string emailId, CancellationToken cancellationToken = default) =>
        throw new EmailUnavailableException("Email is not available yet.");
}
