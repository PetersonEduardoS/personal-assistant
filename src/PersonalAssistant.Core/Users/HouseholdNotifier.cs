using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Users;

/// <summary>Who in the household got a notice now, and who will only see it later.</summary>
public sealed record HouseholdNotice(IReadOnlyList<string> Notified, IReadOnlyList<string> NotNotified);

/// <summary>
/// Tells the other people of a household about something shared, such as an appointment for both.
/// It only writes to people whose 24-hour window is open, so no template is needed; the others
/// see it later in the daily brief and the reminder. A failed send never breaks the caller.
/// </summary>
public sealed class HouseholdNotifier(
    UserDirectory users,
    IConversationStore conversations,
    IMessageSender sender,
    TimeProvider clock)
{
    public IReadOnlyList<UserProfile> MembersOf(string? household) =>
        string.IsNullOrWhiteSpace(household)
            ? []
            : users.All.Where(user => user.Household == household).ToList();

    public async Task<HouseholdNotice> NotifyOthersAsync(
        UserProfile from,
        string text,
        CancellationToken cancellationToken = default)
    {
        var notified = new List<string>();
        var notNotified = new List<string>();

        foreach (var member in MembersOf(from.Household).Where(member => member.PhoneNumber != from.PhoneNumber))
        {
            try
            {
                var lastMessage = await conversations.GetLastUserMessageAtAsync(member.PhoneNumber, cancellationToken);

                if (!CustomerServiceWindow.IsOpen(lastMessage, clock.GetUtcNow()))
                {
                    notNotified.Add(member.Name);
                    continue;
                }

                await sender.SendTextAsync(member.PhoneNumber, text, cancellationToken);
                await conversations.AddAssistantReplyAsync(member.PhoneNumber, text, cancellationToken);
                notified.Add(member.Name);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                notNotified.Add(member.Name);
            }
        }

        return new HouseholdNotice(notified, notNotified);
    }

    /// <summary>A sentence for the assistant, saying who was told now and who was not.</summary>
    public static string Describe(HouseholdNotice notice)
    {
        var parts = new List<string>();

        if (notice.Notified.Count > 0)
        {
            parts.Add($"Avisei: {string.Join(", ", notice.Notified)}.");
        }

        foreach (var name in notice.NotNotified)
        {
            parts.Add($"{name} não recebeu aviso agora (sem conversa nas últimas 24 horas) e vai ver no resumo diário e no lembrete.");
        }

        return string.Join(" ", parts);
    }
}
