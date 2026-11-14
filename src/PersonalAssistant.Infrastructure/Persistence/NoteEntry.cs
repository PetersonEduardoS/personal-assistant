namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>A stored fact the user asked the assistant to remember.</summary>
public sealed class NoteEntry
{
    public long Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
