using System.Globalization;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.MorningBriefs;

internal sealed class SetMorningBriefTimeTool(IMorningBriefPreferenceStore preferences, string phoneNumber) : IAssistantTool
{
    private const string TimeFormat = "HH:mm";

    public string Name => "set_morning_brief_time";

    public string Description =>
        "Muda o resumo diário da pessoa. Use mode \"time\" com time em HH:mm quando ela pedir um horário " +
        $"(entre {Format(MorningBriefPreference.EarliestTime)} e {Format(MorningBriefPreference.LatestTime)}), " +
        "mode \"off\" quando ela não quiser mais receber, e mode \"automatic\" quando ela quiser voltar ao horário " +
        "automático ou padrão. O horário escolhido aqui vale mais do que o horário automático. " +
        "Se o horário pedido estiver fora da faixa, pergunte outro. Depois, confirme em uma frase curta.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mode": { "type": "string", "enum": ["time", "off", "automatic"] },
            "time": { "type": "string", "description": "Hora no formato HH:mm, 24 horas, só com mode time" }
          },
          "required": ["mode"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        switch (ReadString(input, "mode"))
        {
            case "time":
                var time = ReadTime(input);
                await preferences.SetAsync(phoneNumber, MorningBriefPreference.At(time), cancellationToken);
                return $"Pronto: o resumo diário vai chegar todo dia às {Format(time)}. " +
                    "Se o resumo de hoje já foi enviado, a mudança vale a partir de amanhã.";

            case "off":
                await preferences.SetAsync(phoneNumber, MorningBriefPreference.Off, cancellationToken);
                return "Pronto: o resumo diário foi desligado. Para voltar, basta pedir um horário.";

            case "automatic":
                await preferences.ClearAsync(phoneNumber, cancellationToken);
                return "Pronto: o resumo diário voltou ao horário padrão.";

            default:
                throw new AssistantToolException("O campo mode deve ser time, off ou automatic.");
        }
    }

    private static TimeOnly ReadTime(JsonElement input)
    {
        var text = ReadString(input, "time")
            ?? throw new AssistantToolException("Com mode time, informe o campo time no formato HH:mm.");

        if (!TimeOnly.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new AssistantToolException("O campo time deve ser uma hora válida no formato HH:mm (24 horas).");
        }

        if (time < MorningBriefPreference.EarliestTime || time > MorningBriefPreference.LatestTime)
        {
            throw new AssistantToolException(
                $"O resumo é da manhã: escolha um horário entre {Format(MorningBriefPreference.EarliestTime)} " +
                $"e {Format(MorningBriefPreference.LatestTime)}. Pergunte outro horário à pessoa.");
        }

        return time;
    }

    private static string? ReadString(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    private static string Format(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);
}
