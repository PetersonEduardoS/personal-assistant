namespace PersonalAssistant.Core.Notes;

/// <summary>
/// A lasting fact the user asked the assistant to remember, such as their work hours or a preference.
/// </summary>
public sealed record UserNote(long Id, string Content, DateTimeOffset CreatedAt)
{
    public const int MaxContentLength = 300;

    /// <summary>Notes go into every message, so their number is capped to keep each message cheap.</summary>
    public const int MaxNotesPerUser = 60;
}
