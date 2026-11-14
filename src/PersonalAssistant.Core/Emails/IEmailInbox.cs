using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Emails;

/// <summary>
/// Reads a user's inbox. It never sends, deletes, moves or marks emails as read.
/// Failures throw <see cref="EmailUnavailableException"/>.
/// </summary>
public interface IEmailInbox
{
    bool IsConfigured(UserProfile user);

    /// <summary>Lists recent inbox emails, newest first, leaving promotions out.</summary>
    Task<IReadOnlyList<EmailSummary>> ListRecentAsync(
        UserProfile user,
        EmailQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the email, or null when it does not exist.</summary>
    Task<EmailMessage?> ReadAsync(UserProfile user, string emailId, CancellationToken cancellationToken = default);
}
