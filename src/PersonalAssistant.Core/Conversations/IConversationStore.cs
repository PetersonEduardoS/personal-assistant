using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.Conversations;

/// <summary>
/// Keeps the conversation history of each user.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Saves a received message. Returns false when it was already saved,
    /// which happens when Meta delivers the same webhook more than once.
    /// </summary>
    Task<bool> TryAddIncomingMessageAsync(
        string phoneNumber,
        string whatsAppMessageId,
        string content,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);

    Task AddAssistantReplyAsync(string phoneNumber, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest turns in chronological order, always starting with a user turn.
    /// </summary>
    Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(
        string phoneNumber,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns when the user last sent a message, or null when they never did.
    /// Used to know whether the 24-hour window is open.
    /// </summary>
    Task<DateTimeOffset?> GetLastUserMessageAtAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
