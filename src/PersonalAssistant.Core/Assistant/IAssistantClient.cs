namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// Gets a reply from the AI assistant for a user message.
/// </summary>
public interface IAssistantClient
{
    Task<string> GetReplyAsync(string userMessage, CancellationToken cancellationToken = default);
}
