using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Core.Emails;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Infrastructure.Emails;

namespace PersonalAssistant.Infrastructure.Tests.Emails;

public class ImapEmailInboxTests
{
    private const string Password = "super-secret-app-password";

    private readonly ImapEmailInbox _inbox = new(NullLogger<ImapEmailInbox>.Instance);

    [Fact]
    public void IsConfigured_RequiresHostUserAndPassword()
    {
        Assert.True(_inbox.IsConfigured(UserWith(new UserEmailSettings
        {
            ImapHost = "imap.gmail.com", Username = "eduardo@example.com", AppPassword = Password
        })));
        Assert.False(_inbox.IsConfigured(UserWith(new UserEmailSettings
        {
            ImapHost = "imap.gmail.com", Username = "eduardo@example.com"
        })));
        Assert.False(_inbox.IsConfigured(UserWith(null)));
    }

    [Fact]
    public async Task ListRecentAsync_ThrowsEmailUnavailable_WithoutTheSecret_WhenTheServerIsUnreachable()
    {
        var user = UserWith(new UserEmailSettings
        {
            ImapHost = "127.0.0.1", Port = 1, Username = "eduardo@example.com", AppPassword = Password
        });

        var exception = await Assert.ThrowsAsync<EmailUnavailableException>(() =>
            _inbox.ListRecentAsync(user, new EmailQuery(DateTimeOffset.UtcNow.AddDays(-2), null, null, 15)));

        Assert.DoesNotContain(Password, exception.Message);
    }

    [Fact]
    public async Task ReadAsync_ThrowsEmailUnavailable_WhenTheUserHasNoEmail()
    {
        await Assert.ThrowsAsync<EmailUnavailableException>(() => _inbox.ReadAsync(UserWith(null), "1"));
    }

    private static UserProfile UserWith(UserEmailSettings? email) =>
        new() { PhoneNumber = "353800000001", Name = "Eduardo", Email = email };
}
