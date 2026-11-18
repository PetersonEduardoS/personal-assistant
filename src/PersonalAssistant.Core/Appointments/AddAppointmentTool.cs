using System.Globalization;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Appointments;

internal sealed class AddAppointmentTool(
    IAppointmentStore store,
    UserProfile user,
    HouseholdNotifier household,
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
        "Use shared true só quando a pessoa disser que o compromisso é dos dois (\"pros dois\", \"nós dois\", " +
        "\"eu e a Karina\", \"eu e o Eduardo\"); aí ele aparece para o casal e o lembrete vai para os dois. " +
        "Na dúvida, pergunte. " +
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
            },
            "shared": { "type": "boolean", "description": "true quando o compromisso é do casal" }
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
        var shared = input.ValueKind == JsonValueKind.Object
            && input.TryGetProperty("shared", out var sharedValue)
            && sharedValue.ValueKind == JsonValueKind.True;

        if (shared && string.IsNullOrWhiteSpace(user.Household))
        {
            throw new AssistantToolException(
                "Esta pessoa não divide a casa com ninguém no assistente, então não dá para marcar pros dois. " +
                "Salve como compromisso dela.");
        }

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
                user.PhoneNumber, title, date, time, reminderMinutesBefore,
                shared ? user.Household : null, user.Name, cancellationToken);

            var forBoth = appointment.IsShared ? "pros dois, " : string.Empty;
            var saved = $"Salvo: {AppointmentText.Describe(appointment)} " +
                $"({forBoth}{ReminderText.DescribeSetting(appointment.ReminderMinutesBefore)}) (id {appointment.Id}).";

            if (!appointment.IsShared)
            {
                return saved;
            }

            var notice = await household.NotifyOthersAsync(
                user, $"📅 {user.Name} marcou pros dois: {AppointmentText.Describe(appointment)}.", cancellationToken);

            return $"{saved} {HouseholdNotifier.Describe(notice)}".TrimEnd();
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
}
