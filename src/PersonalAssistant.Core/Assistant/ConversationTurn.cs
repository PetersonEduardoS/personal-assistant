namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// One message in a conversation, written by the user or by the assistant.
/// </summary>
public sealed record ConversationTurn(ConversationRole Role, string Content);
