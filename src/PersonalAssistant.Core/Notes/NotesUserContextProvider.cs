using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Notes;

/// <summary>
/// Adds the notes saved from the chat after the user's context file, so the assistant
/// knows them in every conversation and in the daily brief.
/// </summary>
public sealed class NotesUserContextProvider(IUserContextProvider fileContext, INoteStore notes) : IUserContextProvider
{
    public async Task<string?> GetContextAsync(UserProfile user, CancellationToken cancellationToken = default)
    {
        var context = await fileContext.GetContextAsync(user, cancellationToken);
        var saved = await notes.ListAsync(user.PhoneNumber, cancellationToken);

        if (saved.Count == 0)
        {
            return context;
        }

        var section = NoteText.ContextHeader + "\n" +
            string.Join("\n", saved.Select(note => $"- {NoteText.Describe(note)}"));

        return string.IsNullOrWhiteSpace(context) ? section : $"{context}\n\n{section}";
    }
}
