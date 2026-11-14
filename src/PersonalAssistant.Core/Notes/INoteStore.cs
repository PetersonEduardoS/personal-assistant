namespace PersonalAssistant.Core.Notes;

/// <summary>
/// Keeps the notes of each user. A user only sees and deletes their own notes.
/// </summary>
public interface INoteStore
{
    /// <summary>
    /// Saves a note. The text is trimmed; an empty or too long note throws <see cref="ArgumentException"/>.
    /// </summary>
    Task<UserNote> AddAsync(string phoneNumber, string content, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's notes, oldest first.</summary>
    Task<IReadOnlyList<UserNote>> ListAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the note and returns it, or returns null when it does not exist or belongs to someone else.
    /// </summary>
    Task<UserNote?> DeleteAsync(string phoneNumber, long noteId, CancellationToken cancellationToken = default);
}
