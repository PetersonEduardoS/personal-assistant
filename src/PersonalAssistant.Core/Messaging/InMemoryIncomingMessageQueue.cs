using System.Threading.Channels;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Messaging;

public sealed class InMemoryIncomingMessageQueue : IIncomingMessageQueue
{
    private readonly Channel<IncomingMessage> _channel = Channel.CreateUnbounded<IncomingMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(IncomingMessage message, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<IncomingMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
