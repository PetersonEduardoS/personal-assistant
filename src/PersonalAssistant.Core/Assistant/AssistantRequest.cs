namespace PersonalAssistant.Core.Assistant;

/// <summary>A photo or PDF sent with the latest message.</summary>
public sealed record AssistantAttachment(byte[] Content, string MimeType);

/// <summary>
/// Everything the assistant needs to answer: who is talking, the conversation so far,
/// optional background about that person, the tools the assistant may use for them
/// and the photo or PDF that came with the latest message, if any.
/// </summary>
public sealed record AssistantRequest(
    string UserName,
    IReadOnlyList<ConversationTurn> Conversation,
    string? UserContext = null,
    IReadOnlyList<IAssistantTool>? Tools = null,
    AssistantAttachment? Attachment = null);
