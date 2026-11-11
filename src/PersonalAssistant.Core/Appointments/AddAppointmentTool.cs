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
        "Depois de salvar, confirme para a pessoa exatamente no formato \"Título dia dd/MM às HH:mm\", " +
        "por exemplo \"Corte de cabelo dia 13/11 às 13:30\".";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Título curto, por exemplo Corte de cabelo" },
            "date": { "type": "string", "description": "Dia no formato AAAA-MM-DD" },
            "time": { "type": "string", "description": "Hora no formato HH:mm, 24 horas" }
          },
          "required": ["title", "date", "time"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var title = AppointmentText.GetRequiredString(input, "title");
        var date = AppointmentText.ParseDate(AppointmentText.GetRequiredString(input, "date"), "date");
        var time = AppointmentText.ParseTime(AppointmentText.GetRequiredString(input, "time"));

        if (date.ToDateTime(time) < AppointmentText.LocalNow(clock, timeZone))
        {
            throw new AssistantToolException(
                $"O dia {date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} às " +
                $"{time.ToString(AppointmentText.TimeFormat, CultureInfo.InvariantCulture)} já passou. " +
                "Pergunte à pessoa qual é o dia certo.");
        }

        try
        {
            var appointment = await store.AddAsync(phoneNumber, title, date, time, cancellationToken);

            return $"Salvo: {AppointmentText.Describe(appointment)} (id {appointment.Id}).";
        }
        catch (ArgumentException)
        {
            throw new AssistantToolException(
                $"O título precisa ter entre 1 e {Appointment.MaxTitleLength} caracteres.");
        }
    }
}
