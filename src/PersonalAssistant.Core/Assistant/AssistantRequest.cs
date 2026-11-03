namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// Everything the assistant needs to answer: who is talking, the conversation so far
/// and optional background about that person.
/// </summary>
public sealed record AssistantRequest(
    string UserName,
    IReadOnlyList<ConversationTurn> Conversation,
    string? UserContext = null);
