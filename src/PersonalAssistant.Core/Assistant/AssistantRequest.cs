namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// Everything the assistant needs to answer: who is talking, the conversation so far,
/// optional background about that person and the tools the assistant may use for them.
/// </summary>
public sealed record AssistantRequest(
    string UserName,
    IReadOnlyList<ConversationTurn> Conversation,
    string? UserContext = null,
    IReadOnlyList<IAssistantTool>? Tools = null);
