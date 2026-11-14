using System.Text.Json;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.Notes;

internal sealed class SaveNoteTool(INoteStore store, string phoneNumber) : IAssistantTool
{
    public string Name => "save_note";

    public string Description =>
        "Salva um fato duradouro sobre a pessoa, para lembrar em todas as conversas futuras: trabalho e horários, " +
        "estudo, agenda fixa, preferências, gostos, pessoas próximas. Use quando a pessoa disser \"Lembra que...\" " +
        "ou contar algo que vale lembrar sempre. Não use para compromissos com dia e hora (use add_appointment) " +
        "nem para coisas passageiras. Uma nota por fato, em uma frase curta e completa, que faça sentido sozinha, " +
        "por exemplo \"Trabalha na loja X de segunda a sexta, das 9:00 às 17:00.\". " +
        "Se o fato substitui uma nota antiga, apague a antiga com delete_note. " +
        "Depois, confirme em uma frase curta o que foi anotado.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "content": { "type": "string", "description": "O fato, em uma frase curta e completa" }
          },
          "required": ["content"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var content = input.ValueKind == JsonValueKind.Object
            && input.TryGetProperty("content", out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()!.Trim()
                : string.Empty;

        if (content.Length == 0)
        {
            throw new AssistantToolException("Falta o campo content com o fato a anotar.");
        }

        if (content.Length > UserNote.MaxContentLength)
        {
            throw new AssistantToolException(
                $"A nota pode ter no máximo {UserNote.MaxContentLength} caracteres. Resuma ou divida em mais notas.");
        }

        var existing = await store.ListAsync(phoneNumber, cancellationToken);

        if (existing.Count >= UserNote.MaxNotesPerUser)
        {
            throw new AssistantToolException(
                $"A pessoa já tem {UserNote.MaxNotesPerUser} notas, o limite. Use list_notes e delete_note " +
                "para apagar ou juntar notas antigas antes de salvar outra.");
        }

        var note = await store.AddAsync(phoneNumber, content, cancellationToken);

        return $"Anotado: {NoteText.Describe(note)}.";
    }
}
