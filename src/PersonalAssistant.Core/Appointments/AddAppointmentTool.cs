using System.Globalization;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.Appointments;

internal sealed class AddAppointmentTool(
    IAppointmentStore store,
    string phoneNumber,
    TimeProvider clock,
    TimeZoneInfo timeZone) : IAssistantTool
{
    public string Name => "add_appointment";

    public string Description =>
        "Salva um compromisso da pessoa, como corte de cabelo, café com alguém ou consulta. " +
        "Use só quando tiver título, dia e hora claros. Se faltar algum deles ou houver dúvida, pergunte antes " +
        "e não chute. Converta dias relativos (amanhã, sexta, semana que vem) usando a data atual informada. " +
        "Use um título curto e limpo, com letra maiúscula no início. " +
        "O lembrete padrão é 1 hora antes. Se a pessoa pedir outro (\"me lembra 2 horas antes\"), " +
        "use remind_minutes_before; se ela não quiser lembrete, use 0. " +
        "Depois de salvar, confirme para a pessoa no formato \"Título dia dd/MM às HH:mm (lembrete ...)\", " +
        "por exemplo \"Corte de cabelo dia 13/11 às 13:30 (lembrete 1 hora antes)\".";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Título curto, por exemplo Corte de cabelo" },
            "date": { "type": "string", "description": "Dia no formato AAAA-MM-DD" },
            "time": { "type": "string", "description": "Hora no formato HH:mm, 24 horas" },
            "remind_minutes_before": {
              "type": "integer",
              "description": "Minutos de antecedência do lembrete, de 0 a 1440. 0 = sem lembrete. Se omitido, 60."
            }
          },
          "required": ["title", "date", "time"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var title = AppointmentText.GetRequiredString(input, "title");
        var date = AppointmentText.ParseDate(AppointmentText.GetRequiredString(input, "date"), "date");
        var time = AppointmentText.ParseTime(AppointmentText.GetRequiredString(input, "time"));
        var reminderMinutesBefore = ReadReminder(input);

        if (date.ToDateTime(time) < AppointmentText.LocalNow(clock, timeZone))
        {
            throw new AssistantToolException(
                $"O dia {date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} às " +
                $"{time.ToString(AppointmentText.TimeFormat, CultureInfo.InvariantCulture)} já passou. " +
                "Pergunte à pessoa qual é o dia certo.");
        }

        try
        {
            var appointment = await store.AddAsync(
                phoneNumber, title, date, time, reminderMinutesBefore, cancellationToken);

            return $"Salvo: {AppointmentText.Describe(appointment)} " +
                $"({DescribeReminder(appointment.ReminderMinutesBefore)}) (id {appointment.Id}).";
        }
        catch (ArgumentException)
        {
            throw new AssistantToolException(
                $"O título precisa ter entre 1 e {Appointment.MaxTitleLength} caracteres.");
        }
    }

    private static int? ReadReminder(JsonElement input)
    {
        if (!input.TryGetProperty("remind_minutes_before", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return Appointment.DefaultReminderMinutes;
        }

        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var minutes)
            || minutes < 0
            || minutes > Appointment.MaxReminderMinutes)
        {
            throw new AssistantToolException(
                $"remind_minutes_before precisa ser um número inteiro de 0 a {Appointment.MaxReminderMinutes}.");
        }

        return minutes == 0 ? null : minutes;
    }

    internal static string DescribeReminder(int? minutesBefore)
    {
        if (minutesBefore is null)
        {
            return "sem lembrete";
        }

        var hours = minutesBefore.Value / 60;
        var minutes = minutesBefore.Value % 60;
        var parts = new List<string>();

        if (hours > 0)
        {
            parts.Add(hours == 1 ? "1 hora" : $"{hours} horas");
        }

        if (minutes > 0)
        {
            parts.Add(minutes == 1 ? "1 minuto" : $"{minutes} minutos");
        }

        return $"lembrete {string.Join(" e ", parts)} antes";
    }
}
