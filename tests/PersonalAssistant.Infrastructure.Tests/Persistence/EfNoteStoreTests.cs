using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class EfNoteStoreTests : IDisposable
{
    private const string Eduardo = "353800000001";
    private const string Karina = "353800000002";

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task AddAsync_StoresATrimmedNote_WithIdAndDate()
    {
        await using var context = _database.CreateContext();
        var store = new EfNoteStore(context, _clock);

        var added = await store.AddAsync(Karina, "  Gosta de respostas curtas.  ");

        Assert.True(added.Id > 0);
        Assert.Equal("Gosta de respostas curtas.", added.Content);
        Assert.Equal(_clock.GetUtcNow(), added.CreatedAt);
        Assert.Equal(added, Assert.Single(await store.ListAsync(Karina)));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AddAsync_RejectsAnEmptyOrTooLongNote(string? content)
    {
        await using var context = _database.CreateContext();
        var store = new EfNoteStore(context, _clock);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            store.AddAsync(Karina, content ?? new string('a', PersonalAssistant.Core.Notes.UserNote.MaxContentLength + 1)));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyThatUsersNotes_InOrder()
    {
        await using var context = _database.CreateContext();
        var store = new EfNoteStore(context, _clock);
        await store.AddAsync(Karina, "Primeira");
        await store.AddAsync(Eduardo, "Do Eduardo");
        await store.AddAsync(Karina, "Segunda");

        var notes = await store.ListAsync(Karina);

        Assert.Equal(new[] { "Primeira", "Segunda" }, notes.Select(note => note.Content).ToArray());
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheOwnersNote()
    {
        await using var context = _database.CreateContext();
        var store = new EfNoteStore(context, _clock);
        var note = await store.AddAsync(Karina, "Trabalha às segundas.");

        Assert.Null(await store.DeleteAsync(Eduardo, note.Id));
        Assert.Equal(note, await store.DeleteAsync(Karina, note.Id));
        Assert.Empty(await store.ListAsync(Karina));
        Assert.Null(await store.DeleteAsync(Karina, note.Id));
    }
}
