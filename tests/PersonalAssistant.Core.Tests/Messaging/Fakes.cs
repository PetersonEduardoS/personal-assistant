using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.MorningBriefs;

namespace PersonalAssistant.Core.Tests.Messaging;

internal sealed class FakeMorningBriefStore : IMorningBriefStore
{
    public Dictionary<(string Phone, DateOnly Date), MorningBrief> Briefs { get; } = [];
    public Task<MorningBrief?> GetAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(Briefs.GetValueOrDefault((phoneNumber, date)));
    public Task<bool> TryAddAsync(string phoneNumber, DateOnly date, string content, CancellationToken cancellationToken = default) =>
        Task.FromResult(Briefs.TryAdd((phoneNumber, date), new(phoneNumber, date, content, null)));
    public Task<bool> TryMarkDeliveredAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!Briefs.TryGetValue((phoneNumber, date), out var brief) || brief.IsDelivered) return Task.FromResult(false);
        Briefs[(phoneNumber, date)] = brief with { DeliveredAt = DateTimeOffset.UtcNow };
        return Task.FromResult(true);
    }
}

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
    public Task SendTemplateAsync(string recipient, string templateName, string languageCode,
        IReadOnlyList<string>? bodyParameters = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

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
    private readonly Dictionary<string, DateTimeOffset> _lastUserMessageAt = [];

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

        if (!_lastUserMessageAt.TryGetValue(phoneNumber, out var lastSentAt) || receivedAt > lastSentAt)
        {
            _lastUserMessageAt[phoneNumber] = receivedAt;
        }

        return Task.FromResult(true);
    }

    public Task<DateTimeOffset?> GetLastUserMessageAtAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? lastSentAt = _lastUserMessageAt.TryGetValue(phoneNumber, out var sentAt) ? sentAt : null;

        return Task.FromResult(lastSentAt);
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

/// <summary>
/// A clock that always returns the same time, so tests do not depend on the real time.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
