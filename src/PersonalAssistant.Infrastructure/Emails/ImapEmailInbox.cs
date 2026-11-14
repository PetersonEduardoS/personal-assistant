using System.Globalization;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using PersonalAssistant.Core.Emails;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Infrastructure.Emails;

/// <summary>
/// Reads the user's inbox over IMAP with an app password. The inbox is opened read only,
/// so nothing is ever marked as read, moved or deleted. The password is never logged.
/// </summary>
public sealed class ImapEmailInbox(ILogger<ImapEmailInbox> logger) : IEmailInbox
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public bool IsConfigured(UserProfile user) => user.Email?.IsComplete == true;

    public Task<IReadOnlyList<EmailSummary>> ListRecentAsync(
        UserProfile user,
        EmailQuery query,
        CancellationToken cancellationToken = default) =>
        UseInboxAsync<IReadOnlyList<EmailSummary>>(user, async (settings, inbox) =>
        {
            var search = IsGmail(settings)
                ? SearchQuery.GMailRawSearch(GmailSearch.Build(query))
                : StandardSearch(query);

            var ids = await inbox.SearchAsync(search, cancellationToken);
            var newest = ids.OrderByDescending(id => id.Id).Take(query.MaxCount).ToList();

            if (newest.Count == 0)
            {
                return [];
            }

            var summaries = await inbox.FetchAsync(
                newest,
                MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags
                    | MessageSummaryItems.InternalDate | MessageSummaryItems.PreviewText,
                cancellationToken);

            return summaries
                .Select(ToSummary)
                .Where(email => email.ReceivedAt >= query.Since)
                .OrderByDescending(email => email.ReceivedAt)
                .ToList();
        }, cancellationToken);

    public Task<EmailMessage?> ReadAsync(UserProfile user, string emailId, CancellationToken cancellationToken = default) =>
        UseInboxAsync<EmailMessage?>(user, async (_, inbox) =>
        {
            if (!uint.TryParse(emailId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
            {
                return null;
            }

            MimeMessage message;

            try
            {
                message = await inbox.GetMessageAsync(new UniqueId(id), cancellationToken);
            }
            catch (MessageNotFoundException)
            {
                return null;
            }

            var sender = message.From.Mailboxes.FirstOrDefault();

            return new EmailMessage(
                emailId,
                EmailContent.FormatSender(sender?.Name, sender?.Address),
                SubjectOrDefault(message.Subject),
                message.Date,
                EmailContent.ToPlainText(message.TextBody, message.HtmlBody));
        }, cancellationToken);

    private async Task<T> UseInboxAsync<T>(
        UserProfile user,
        Func<UserEmailSettings, IMailFolder, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (user.Email is not { IsComplete: true } settings)
        {
            throw new EmailUnavailableException("Email is not configured for this user.");
        }

        using var client = new ImapClient { Timeout = (int)Timeout.TotalMilliseconds };

        try
        {
            await client.ConnectAsync(settings.ImapHost, settings.Port, SecureSocketOptions.SslOnConnect, cancellationToken);
            await client.AuthenticateAsync(settings.Username, settings.AppPassword, cancellationToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            var result = await action(settings, inbox);
            await client.DisconnectAsync(true, cancellationToken);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Only the error type: messages from the server must never put credentials in the logs.
            logger.LogWarning("Could not read the mailbox of {UserName}: {ErrorType}", user.Name, exception.GetType().Name);

            throw new EmailUnavailableException("The mailbox could not be read.", exception);
        }
    }

    /// <summary>Gmail understands its own search syntax, which can leave promotions out.</summary>
    private static bool IsGmail(UserEmailSettings settings) =>
        settings.ImapHost.Trim().Equals("imap.gmail.com", StringComparison.OrdinalIgnoreCase);

    private static SearchQuery StandardSearch(EmailQuery query)
    {
        SearchQuery search = SearchQuery.DeliveredAfter(query.Since.UtcDateTime.Date.AddDays(-1));

        if (!string.IsNullOrWhiteSpace(query.From))
        {
            search = search.And(SearchQuery.FromContains(query.From));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            search = search.And(SearchQuery.SubjectContains(query.Text).Or(SearchQuery.BodyContains(query.Text)));
        }

        return search;
    }

    private static EmailSummary ToSummary(IMessageSummary summary)
    {
        var sender = summary.Envelope?.From?.Mailboxes.FirstOrDefault();

        return new EmailSummary(
            summary.UniqueId.Id.ToString(CultureInfo.InvariantCulture),
            EmailContent.FormatSender(sender?.Name, sender?.Address),
            SubjectOrDefault(summary.Envelope?.Subject),
            summary.InternalDate ?? summary.Envelope?.Date ?? DateTimeOffset.MinValue,
            EmailContent.Snippet(summary.PreviewText, EmailLimits.SnippetLength),
            !(summary.Flags?.HasFlag(MessageFlags.Seen) ?? false));
    }

    private static string SubjectOrDefault(string? subject) =>
        string.IsNullOrWhiteSpace(subject) ? "(sem assunto)" : subject.Trim();
}
