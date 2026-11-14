using System.Text.Json;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.Notes;

internal sealed class DeleteNoteTool(INoteStore store, string phoneNumber) : IAssistantTool
{
    public string Name => "delete_note";

    public string Description =>
        "Apaga (esquece) uma nota da pessoa pelo id. Use quando ela pedir para esquecer algo ou quando um fato " +
        "mudou e a nota antiga ficou errada. Os ids aparecem no contexto e em list_notes; nunca chute o id.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "note_id": { "type": "integer", "description": "O id da nota" }
          },
          "required": ["note_id"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("note_id", out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var noteId))
        {
            throw new AssistantToolException("Falta o campo note_id, que deve ser um número.");
        }

        var deleted = await store.DeleteAsync(phoneNumber, noteId, cancellationToken)
            ?? throw new AssistantToolException(
                $"Não existe nota com o id {noteId} para esta pessoa. Use list_notes para ver os ids.");

        return $"Esquecido: {deleted.Content}";
    }
}
