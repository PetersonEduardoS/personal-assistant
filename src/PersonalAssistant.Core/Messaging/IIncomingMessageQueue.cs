using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Holds received messages until the background worker processes them,
/// so the webhook can answer Meta right away.
/// </summary>
public interface IIncomingMessageQueue
{
    ValueTask EnqueueAsync(IncomingMessage message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<IncomingMessage> ReadAllAsync(CancellationToken cancellationToken = default);
}
