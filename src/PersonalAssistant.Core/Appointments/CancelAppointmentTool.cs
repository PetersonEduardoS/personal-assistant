using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Appointments;

internal sealed class CancelAppointmentTool(
    IAppointmentStore store,
    UserProfile user,
    HouseholdNotifier household) : IAssistantTool
{
    public string Name => "cancel_appointment";

    public string Description =>
        "Cancela (apaga) um compromisso salvo da pessoa, ou um compromisso dos dois, pelo id. Antes, use list_appointments para achar o id certo; " +
        "nunca chute o id. Se houver mais de um compromisso parecido, pergunte qual. " +
        "Depois, confirme no formato \"Cancelado: Título dia dd/MM às HH:mm\".";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "appointment_id": { "type": "integer", "description": "O id mostrado por list_appointments" }
          },
          "required": ["appointment_id"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var appointmentId = ReadId(input);
        var cancelled = await store.CancelAsync(user.PhoneNumber, appointmentId, user.Household, cancellationToken)
            ?? throw new AssistantToolException(
                $"Não existe compromisso com o id {appointmentId} para esta pessoa. Use list_appointments para ver os ids.");

        var result = $"Cancelado: {AppointmentText.Describe(cancelled)}.";

        if (!cancelled.IsShared)
        {
            return result;
        }

        var notice = await household.NotifyOthersAsync(
            user,
            $"🗑️ {user.Name} cancelou o compromisso de vocês dois: {AppointmentText.Describe(cancelled)}.",
            cancellationToken);

        return $"{result} {HouseholdNotifier.Describe(notice)}".TrimEnd();
    }

    private static long ReadId(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty("appointment_id", out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        throw new AssistantToolException("Falta o campo appointment_id, que deve ser um número.");
    }
}
