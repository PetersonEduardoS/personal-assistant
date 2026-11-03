using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>
/// A stored message from the conversation between a user and the assistant.
/// </summary>
public sealed class ConversationMessage
{
    public long Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public ConversationRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The WhatsApp ID of a received message. Empty for the assistant's replies.
    /// It is unique, so a webhook delivered twice is stored only once.
    /// </summary>
    public string? WhatsAppMessageId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
