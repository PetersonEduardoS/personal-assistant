using System.Globalization;
using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Deadlines;

internal sealed class ListDeadlinesTool(
    IDeadlineProvider deadlines,
    UserProfile user,
    TimeProvider clock,
    TimeZoneInfo timeZone) : IAssistantTool
{
    public string Name => "list_deadlines";

    public string Description =>
        "Mostra os prazos da faculdade da pessoa (entregas e quizzes do Moodle) " +
        $"dos próximos {UpcomingDeadlines.HorizonDays} dias, com matéria, dia e hora. " +
        "Use para perguntas sobre entregas, trabalhos, prazos ou \"o que eu tenho essa semana\". " +
        "Escreva os títulos em português ao responder (por exemplo, \"entrega do Integrated CA2\" em vez de \"is due\").";

    public string InputSchema => """{ "type": "object", "properties": {} }""";

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(AppointmentText.LocalNow(clock, timeZone));
        var upcoming = await deadlines.GetUpcomingAsync(user, today, cancellationToken);

        switch (upcoming.Status)
        {
            case UpcomingDeadlinesStatus.NotConfigured:
                return "Esta pessoa não tem calendário da faculdade cadastrado.";
            case UpcomingDeadlinesStatus.Unavailable:
                return "Os prazos da faculdade não puderam ser lidos agora. Diga isso à pessoa e sugira conferir no Moodle.";
        }

        if (upcoming.Deadlines.Count == 0)
        {
            return $"Nenhum prazo da faculdade nos próximos {UpcomingDeadlines.HorizonDays} dias.";
        }

        var result = new StringBuilder($"Prazos da faculdade nos próximos {UpcomingDeadlines.HorizonDays} dias:");

        foreach (var deadline in upcoming.Deadlines)
        {
            var dueDate = DateOnly.FromDateTime(deadline.DueAt.DateTime);
            var course = string.IsNullOrWhiteSpace(deadline.Course) ? string.Empty : $" ({deadline.Course})";
            var time = deadline.DueAt.ToString("HH:mm", CultureInfo.InvariantCulture);

            result.Append($"\n- {deadline.Title}{course}: {PortugueseDates.FormatShort(dueDate)}, às {time}");
        }

        return result.ToString();
    }
}
