using System.Globalization;
using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.WorkSchedules;

internal sealed class ListWorkShiftsTool(
    IWorkScheduleProvider schedules,
    UserProfile user,
    TimeProvider clock,
    TimeZoneInfo timeZone) : IAssistantTool
{
    public const int DefaultDays = 7;
    public const int MaxDays = 14;

    public const string UnpublishedWarning =
        "Atenção: um dia sem turno pode ser folga ou escala ainda não publicada. Não afirme que a pessoa está livre sem dizer isso.";

    public string Name => "list_work_shifts";

    public string Description =>
        "Mostra os turnos de trabalho da pessoa (a escala do trabalho, publicada no Findmyshift), dia a dia, num período. " +
        "Use para perguntas sobre agenda, folgas, disponibilidade ou \"o que eu tenho essa semana\". " +
        $"Sem datas, mostra os próximos {DefaultDays} dias. O período pode ter no máximo {MaxDays} dias.";

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
        var toDate = toText is null ? fromDate.AddDays(DefaultDays - 1) : AppointmentText.ParseDate(toText, "to_date");

        if (toDate < fromDate)
        {
            throw new AssistantToolException("O campo to_date não pode ser antes de from_date.");
        }

        if (toDate.DayNumber - fromDate.DayNumber >= MaxDays)
        {
            throw new AssistantToolException($"O período pode ter no máximo {MaxDays} dias.");
        }

        var result = new StringBuilder("Turnos de trabalho:");

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            var schedule = await schedules.GetScheduleAsync(user, date, cancellationToken);

            switch (schedule.Status)
            {
                case WorkScheduleStatus.NotConfigured:
                    return "Esta pessoa não tem escala de trabalho cadastrada.";
                case WorkScheduleStatus.Unavailable:
                    return "A escala de trabalho não pôde ser lida agora. Diga isso à pessoa e sugira conferir no app.";
            }

            var shifts = schedule.Shifts.Count == 0
                ? "sem turno"
                : string.Join("; ", schedule.Shifts.Select(FormatShift));

            result.Append($"\n- {PortugueseDates.FormatShort(date)}: {shifts}");
        }

        result.Append($"\n{UnpublishedWarning}");

        return result.ToString();
    }

    private static string FormatShift(WorkShift shift)
    {
        var title = string.IsNullOrWhiteSpace(shift.Title) ? "Turno" : shift.Title;
        var start = shift.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
        var end = shift.End.ToString("HH:mm", CultureInfo.InvariantCulture);
        var nextDay = shift.EndsNextDay ? " do dia seguinte" : string.Empty;

        return $"{title} das {start} até as {end}{nextDay}";
    }
}
