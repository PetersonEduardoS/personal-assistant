namespace PersonalAssistant.Core.WhatsApp;

/// <summary>
/// WhatsApp only allows free-form messages within 24 hours of the user's last message.
/// Outside this window, the business can only start a conversation with an approved template.
/// </summary>
public static class CustomerServiceWindow
{
    public static readonly TimeSpan Duration = TimeSpan.FromHours(24);

    public static bool IsOpen(DateTimeOffset? lastUserMessageAt, DateTimeOffset now) =>
        lastUserMessageAt is not null && now - lastUserMessageAt.Value < Duration;
}
