namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Sends a text reply back to a user.
/// </summary>
public interface IMessageSender
{
    Task SendTextAsync(string recipient, string text, CancellationToken cancellationToken = default);
}
