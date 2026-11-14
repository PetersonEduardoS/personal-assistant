using System.Text.Json;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.Notes;

internal sealed class ListNotesTool(INoteStore store, string phoneNumber) : IAssistantTool
{
    public string Name => "list_notes";

    public string Description =>
        "Lista as notas salvas sobre a pessoa, com o id de cada uma. " +
        "Use para responder \"o que você sabe sobre mim?\" ou para achar o id antes de usar delete_note.";

    public string InputSchema => """{ "type": "object", "properties": {} }""";

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var notes = await store.ListAsync(phoneNumber, cancellationToken);

        return notes.Count == 0
            ? "Nenhuma nota salva sobre esta pessoa."
            : string.Join("\n", notes.Select(note => $"- {NoteText.Describe(note)}"));
    }
}
