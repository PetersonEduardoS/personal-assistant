using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.Messaging;

public class InMemoryIncomingMessageQueueTests
{
    [Fact]
    public async Task ReadAllAsync_ReturnsEnqueuedMessages_InOrder()
    {
        var queue = new InMemoryIncomingMessageQueue();
        var first = new IncomingMessage("wamid.TEST1", "353800000001", "Primeira", DateTimeOffset.UnixEpoch);
        var second = new IncomingMessage("wamid.TEST2", "353800000002", "Segunda", DateTimeOffset.UnixEpoch);

        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<IncomingMessage>();

        await foreach (var message in queue.ReadAllAsync(cancellation.Token))
        {
            received.Add(message);

            if (received.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(new[] { first, second }, received);
    }
}
