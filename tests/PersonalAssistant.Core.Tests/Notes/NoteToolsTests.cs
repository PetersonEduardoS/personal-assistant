using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Notes;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Notes;

public class NoteToolsTests
{
    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo" };
    private static readonly UserProfile Karina = new() { PhoneNumber = "353800000002", Name = "Karina" };

    private readonly FakeNoteStore _store = new();

    [Fact]
    public void SaveDescription_ExplainsWhenToSave_AndPointsAppointmentsElsewhere()
    {
        var save = Tool(Eduardo, "save_note");

        Assert.Contains("Lembra que", save.Description);
        Assert.Contains("add_appointment", save.Description);
    }

    [Fact]
    public async Task Save_StoresTheNoteForThatUser_AndConfirms()
    {
        var result = await Run(Karina, "save_note", """{ "content": "  Trabalha de segunda a sexta, das 9:00 às 17:00.  " }""");

        Assert.Contains("Anotado", result);
        var saved = Assert.Single(_store.Notes);
        Assert.Equal(Karina.PhoneNumber, saved.PhoneNumber);
        Assert.Equal("Trabalha de segunda a sexta, das 9:00 às 17:00.", saved.Note.Content);
        Assert.Contains($"nota {saved.Note.Id}", result);
    }

    [Theory]
    [InlineData("""{ "content": "   " }""")]
    [InlineData("""{ }""")]
    public async Task Save_RejectsAnEmptyNote(string input)
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "save_note", input));
        Assert.Empty(_store.Notes);
    }

    [Fact]
    public async Task Save_RejectsATooLongNote()
    {
        var content = new string('a', UserNote.MaxContentLength + 1);

        await Assert.ThrowsAsync<AssistantToolException>(() =>
            Run(Eduardo, "save_note", $$"""{ "content": "{{content}}" }"""));
    }

    [Fact]
    public async Task Save_RejectsANewNote_WhenTheLimitIsReached()
    {
        for (var i = 0; i < UserNote.MaxNotesPerUser; i++)
        {
            await _store.AddAsync(Eduardo.PhoneNumber, $"Fato {i}");
        }

        var exception = await Assert.ThrowsAsync<AssistantToolException>(() =>
            Run(Eduardo, "save_note", """{ "content": "Mais um fato" }"""));

        Assert.Contains("delete_note", exception.Message);
        Assert.Equal(UserNote.MaxNotesPerUser, _store.Notes.Count);
    }

    [Fact]
    public async Task List_ShowsOnlyThatUsersNotes_WithIds()
    {
        var mine = await _store.AddAsync(Karina.PhoneNumber, "Gosta de respostas curtas.");
        await _store.AddAsync(Eduardo.PhoneNumber, "Faz CCT às quartas.");

        var result = await Run(Karina, "list_notes", "{}");

        Assert.Contains($"(nota {mine.Id}) Gosta de respostas curtas.", result);
        Assert.DoesNotContain("CCT", result);
    }

    [Fact]
    public async Task List_SaysWhenThereAreNoNotes()
    {
        Assert.Contains("Nenhuma nota", await Run(Karina, "list_notes", "{}"));
    }

    [Fact]
    public async Task Delete_RemovesTheNote_AndConfirms()
    {
        var note = await _store.AddAsync(Karina.PhoneNumber, "Trabalha às segundas.");

        var result = await Run(Karina, "delete_note", $$"""{ "note_id": {{note.Id}} }""");

        Assert.Contains("Esquecido", result);
        Assert.Contains("Trabalha às segundas.", result);
        Assert.Empty(_store.Notes);
    }

    [Fact]
    public async Task Delete_CannotRemoveAnotherUsersNote()
    {
        var note = await _store.AddAsync(Eduardo.PhoneNumber, "Faz CCT às quartas.");

        await Assert.ThrowsAsync<AssistantToolException>(() =>
            Run(Karina, "delete_note", $$"""{ "note_id": {{note.Id}} }"""));
        Assert.Single(_store.Notes);
    }

    [Fact]
    public async Task Delete_RequiresANumericId()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Karina, "delete_note", """{ "note_id": "x" }"""));
    }

    private IAssistantTool Tool(UserProfile user, string name) =>
        new AssistantToolFactory(
                new FakeAppointmentStore(),
                new FakeWorkScheduleProvider(),
                new FakeDeadlineProvider(),
                new FixedTimeProvider(DateTimeOffset.Parse("2026-11-14T10:00:00Z")),
                TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"),
                _store)
            .CreateFor(user)
            .Single(tool => tool.Name == name);

    private async Task<string> Run(UserProfile user, string name, string input)
    {
        using var document = JsonDocument.Parse(input);

        return await Tool(user, name).ExecuteAsync(document.RootElement);
    }
}
