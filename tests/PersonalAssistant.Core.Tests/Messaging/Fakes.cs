using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Messaging;

namespace PersonalAssistant.Core.Tests.Messaging;

internal sealed class FakeAssistantClient : IAssistantClient
{
    private readonly string? _reply;
    private readonly bool _fails;

    public FakeAssistantClient(string reply) => _reply = reply;

    private FakeAssistantClient() => _fails = true;

    public List<string> ReceivedMessages { get; } = [];

    public static FakeAssistantClient Failing() => new();

    public Task<string> GetReplyAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add(userMessage);

        return _fails
            ? Task.FromException<string>(new InvalidOperationException("Assistant failed"))
            : Task.FromResult(_reply!);
    }
}

internal sealed class FakeMessageSender : IMessageSender
{
    public List<(string Recipient, string Text)> SentMessages { get; } = [];

    public Task SendTextAsync(string recipient, string text, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((recipient, text));

        return Task.CompletedTask;
    }
}
