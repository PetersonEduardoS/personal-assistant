using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Messaging;

internal sealed class FakeAssistantClient : IAssistantClient
{
    private readonly string? _reply;
    private readonly bool _fails;

    public FakeAssistantClient(string reply) => _reply = reply;

    private FakeAssistantClient() => _fails = true;

    public List<AssistantRequest> ReceivedRequests { get; } = [];

    public static FakeAssistantClient Failing() => new();

    public Task<string> GetReplyAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

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

/// <summary>
/// Keeps the conversation in a list, with the same duplicate rule as the real store.
/// </summary>
internal sealed class FakeConversationStore : IConversationStore
{
    private readonly HashSet<string> _messageIds = [];

    public List<(string PhoneNumber, ConversationTurn Turn)> Turns { get; } = [];

    public Task<bool> TryAddIncomingMessageAsync(
        string phoneNumber,
        string whatsAppMessageId,
        string content,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_messageIds.Add(whatsAppMessageId))
        {
            return Task.FromResult(false);
        }

        Turns.Add((phoneNumber, new ConversationTurn(ConversationRole.User, content)));

        return Task.FromResult(true);
    }

    public Task AddAssistantReplyAsync(string phoneNumber, string content, CancellationToken cancellationToken = default)
    {
        Turns.Add((phoneNumber, new ConversationTurn(ConversationRole.Assistant, content)));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(
        string phoneNumber,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ConversationTurn> turns = Turns
            .Where(entry => entry.PhoneNumber == phoneNumber)
            .Select(entry => entry.Turn)
            .TakeLast(limit)
            .ToList();

        return Task.FromResult(turns);
    }
}

internal sealed class FakeUserContextProvider(string? context = null) : IUserContextProvider
{
    public Task<string?> GetContextAsync(UserProfile user, CancellationToken cancellationToken = default) =>
        Task.FromResult(context);
}
