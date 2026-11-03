namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// Gets a reply from the AI assistant for a conversation.
/// </summary>
public interface IAssistantClient
{
    Task<string> GetReplyAsync(AssistantRequest request, CancellationToken cancellationToken = default);
}
