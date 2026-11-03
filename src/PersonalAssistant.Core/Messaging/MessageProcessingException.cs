namespace PersonalAssistant.Core.Messaging;

public sealed class MessageProcessingException(string messageId, Exception innerException)
    : Exception($"Failed to process message {messageId}.", innerException)
{
    public string MessageId { get; } = messageId;
}
