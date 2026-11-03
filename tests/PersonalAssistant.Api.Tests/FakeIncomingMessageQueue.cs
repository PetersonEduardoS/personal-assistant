using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Api.Tests;

/// <summary>
/// Records enqueued messages so tests can check them. Never hands messages to the worker.
/// </summary>
public sealed class FakeIncomingMessageQueue : IIncomingMessageQueue
{
    private readonly ConcurrentQueue<IncomingMessage> _messages = new();

    public IReadOnlyCollection<IncomingMessage> EnqueuedMessages => _messages.ToArray();

    public void Clear() => _messages.Clear();

    public ValueTask EnqueueAsync(IncomingMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<IncomingMessage> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        yield break;
    }
}
