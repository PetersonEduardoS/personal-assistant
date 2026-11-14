using PersonalAssistant.Core.Notes;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Notes;

public class NotesUserContextProviderTests
{
    private static readonly UserProfile Karina = new() { PhoneNumber = "353800000002", Name = "Karina" };

    private readonly FakeNoteStore _notes = new();

    [Theory]
    [InlineData(null)]
    [InlineData("# Karina")]
    public async Task GetContextAsync_KeepsTheFileContext_WhenThereAreNoNotes(string? fileContext)
    {
        var provider = new NotesUserContextProvider(new FakeUserContextProvider(fileContext), _notes);

        Assert.Equal(fileContext, await provider.GetContextAsync(Karina));
    }

    [Fact]
    public async Task GetContextAsync_AddsTheNotes_AfterTheFileContext()
    {
        var first = await _notes.AddAsync(Karina.PhoneNumber, "Trabalha de segunda a sexta.");
        var second = await _notes.AddAsync(Karina.PhoneNumber, "Gosta de respostas curtas.");
        await _notes.AddAsync("353800000001", "Nota de outra pessoa.");
        var provider = new NotesUserContextProvider(new FakeUserContextProvider("# Karina"), _notes);

        var context = await provider.GetContextAsync(Karina);

        Assert.Equal(
            "# Karina\n\n" +
            "## Notas salvas pelo chat\n" +
            $"- (nota {first.Id}) Trabalha de segunda a sexta.\n" +
            $"- (nota {second.Id}) Gosta de respostas curtas.",
            context);
    }

    [Fact]
    public async Task GetContextAsync_ReturnsOnlyTheNotes_WhenThereIsNoFile()
    {
        var note = await _notes.AddAsync(Karina.PhoneNumber, "Gosta de respostas curtas.");
        var provider = new NotesUserContextProvider(new FakeUserContextProvider(), _notes);

        Assert.Equal(
            $"## Notas salvas pelo chat\n- (nota {note.Id}) Gosta de respostas curtas.",
            await provider.GetContextAsync(Karina));
    }
}
