using System.Globalization;
using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Appointments;

internal sealed class ListAppointmentsTool(
    IAppointmentStore store,
    UserProfile user,
    TimeProvider clock,
    TimeZoneInfo timeZone) : IAssistantTool
{
    public const int DefaultDays = 30;
    public const int MaxDays = 60;

    public string Name => "list_appointments";

    public string Description =>
        "Lista os compromissos salvos da pessoa num período, com o id de cada um. " +
        "Use para responder perguntas como \"o que eu tenho na sexta?\" e antes de cancelar, para achar o id certo. " +
        $"Sem datas, lista os próximos {DefaultDays} dias. O período pode ter no máximo {MaxDays} dias.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "from_date": { "type": "string", "description": "Primeiro dia, no formato AAAA-MM-DD" },
            "to_date": { "type": "string", "description": "Último dia (incluído), no formato AAAA-MM-DD" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(AppointmentText.LocalNow(clock, timeZone));
        var fromText = AppointmentText.GetOptionalString(input, "from_date");
        var toText = AppointmentText.GetOptionalString(input, "to_date");

        var fromDate = fromText is null ? today : AppointmentText.ParseDate(fromText, "from_date");
        var toDate = toText is null ? fromDate.AddDays(DefaultDays) : AppointmentText.ParseDate(toText, "to_date");

        if (toDate < fromDate)
        {
            throw new AssistantToolException("O campo to_date não pode ser antes de from_date.");
        }

        if (toDate.DayNumber - fromDate.DayNumber > MaxDays)
        {
            throw new AssistantToolException($"O período pode ter no máximo {MaxDays} dias.");
        }

        var appointments = await store.ListAsync(user.PhoneNumber, fromDate, toDate, user.Household, cancellationToken);
        var period = $"{fromDate.ToString("dd/MM", CultureInfo.InvariantCulture)} e {toDate.ToString("dd/MM", CultureInfo.InvariantCulture)}";

        if (appointments.Count == 0)
        {
            return $"Nenhum compromisso salvo entre {period}.";
        }

        var result = new StringBuilder($"Compromissos entre {period}:");

        foreach (var appointment in appointments)
        {
            result.Append($"\n- {AppointmentText.DescribeWithOwner(appointment)} (id {appointment.Id})");
        }

        return result.ToString();
    }
}
