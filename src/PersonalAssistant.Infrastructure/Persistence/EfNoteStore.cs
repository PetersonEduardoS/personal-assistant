using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.Notes;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class EfNoteStore(AssistantDbContext context, TimeProvider timeProvider) : INoteStore
{
    public async Task<UserNote> AddAsync(string phoneNumber, string content, CancellationToken cancellationToken = default)
    {
        var clean = content.Trim();

        if (clean.Length == 0)
        {
            throw new ArgumentException("The note needs some text.", nameof(content));
        }

        if (clean.Length > UserNote.MaxContentLength)
        {
            throw new ArgumentException(
                $"A note can have at most {UserNote.MaxContentLength} characters.", nameof(content));
        }

        var entry = new NoteEntry
        {
            PhoneNumber = phoneNumber,
            Content = clean,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        context.Notes.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return ToNote(entry);
    }

    public async Task<IReadOnlyList<UserNote>> ListAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var entries = await context.Notes
            .AsNoTracking()
            .Where(entry => entry.PhoneNumber == phoneNumber)
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return entries.Select(ToNote).ToList();
    }

    public async Task<UserNote?> DeleteAsync(string phoneNumber, long noteId, CancellationToken cancellationToken = default)
    {
        var entry = await context.Notes
            .SingleOrDefaultAsync(note => note.Id == noteId && note.PhoneNumber == phoneNumber, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        context.Notes.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);

        return ToNote(entry);
    }

    private static UserNote ToNote(NoteEntry entry) =>
        new(entry.Id, entry.Content, UtcDateTimes.ToOffset(entry.CreatedAtUtc));
}
