namespace PersonalAssistant.Core.WhatsApp;

/// <summary>
/// A text message received from a WhatsApp user.
/// </summary>
public sealed record IncomingMessage(string MessageId, string From, string Text, DateTimeOffset Timestamp);
