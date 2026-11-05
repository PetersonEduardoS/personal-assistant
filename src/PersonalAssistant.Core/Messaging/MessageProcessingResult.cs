namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// What happened to a received message, so the caller can log it.
/// </summary>
public enum MessageProcessingResult
{
    Replied,
    UnknownSender,
    Duplicate,
    TooOld
}
