using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;

namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>Serializes scheduled and reply-triggered delivery within the single application replica.</summary>
public sealed class MorningBriefDeliveryGate
{
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public sealed class MorningBriefDelivery(
    IMorningBriefStore briefs, IMessageSender sender, IConversationStore conversations,
    MorningBriefDeliveryGate gate)
{
    public async Task<bool> TryDeliverAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default)
    {
        await gate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            var brief = await briefs.GetAsync(phoneNumber, date, cancellationToken);
            if (brief is null || brief.IsDelivered) return false;
            await sender.SendTextAsync(phoneNumber, brief.Content, cancellationToken);
            await briefs.TryMarkDeliveredAsync(phoneNumber, date, cancellationToken);
            await conversations.AddAssistantReplyAsync(phoneNumber, brief.Content, cancellationToken);
            return true;
        }
        finally { gate.Semaphore.Release(); }
    }
}
