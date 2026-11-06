using System.Globalization;
using System.Text;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>
/// Asks the assistant to write the brief for one user and one day: weather, agenda, free time,
/// the best time to train, reminders and a short tip.
/// </summary>
public sealed class MorningBriefWriter(IAssistantClient assistantClient, IUserContextProvider userContextProvider)
{
    public const string ShiftsHeader = "Turnos de trabalho de hoje:";

    public const string NoShiftsLine = "Nenhum turno de trabalho hoje.";

    public const string UnknownShiftsLine =
        "A escala de trabalho de hoje não está disponível. Avise isso em uma linha e não diga que o dia está livre de turnos.";

    public async Task<string> WriteAsync(
        UserProfile user,
        DateOnly date,
        WorkSchedule schedule,
        CancellationToken cancellationToken = default)
    {
        var userContext = await userContextProvider.GetContextAsync(user, cancellationToken);
        var instruction = BuildInstruction(user.Name, date, schedule);
        var request = new AssistantRequest(
            user.Name,
            [new ConversationTurn(ConversationRole.User, instruction)],
            userContext);

        return await assistantClient.GetReplyAsync(request, cancellationToken);
    }

    private static string BuildInstruction(string userName, DateOnly date, WorkSchedule schedule)
    {
        var instruction = new StringBuilder();

        instruction.AppendLine(
            $"Monte o resumo do dia de hoje, {PortugueseDates.FormatLong(date)}, para {userName}. " +
            "Ele é enviado de manhã pelo WhatsApp e cobre o dia inteiro.");
        instruction.AppendLine();
        instruction.AppendLine("Inclua, nesta ordem:");
        instruction.AppendLine(
            "1. O tempo em Dublin hoje, separado em manhã, tarde e noite. " +
            "Pesquise a previsão e dê uma dica prática, como levar casaco ou guarda-chuva.");
        instruction.AppendLine(
            "2. A agenda de hoje em ordem de horário: os compromissos do contexto que caem neste dia da semana " +
            "ou nesta data e os turnos de trabalho listados abaixo, se houver. Depois, as janelas livres do dia.");
        instruction.AppendLine(
            "3. Se o contexto falar de academia, o melhor horário para treinar hoje: uma janela livre em que o treino caiba, " +
            "fora do horário de pico, com a unidade mais prática.");
        instruction.AppendLine("4. Lembretes e prazos do contexto que estejam chegando.");
        instruction.AppendLine("5. Uma frase curta de motivação ou uma dica rápida.");

        if (schedule.Status != WorkScheduleStatus.NotConfigured)
        {
            instruction.AppendLine();
            instruction.AppendLine(ShiftsHeader);
            AppendShifts(instruction, schedule);
        }

        instruction.AppendLine();
        instruction.Append(
            "Use só compromissos que estejam no contexto ou na lista de turnos, sem inventar nenhum. " +
            "Seja curto e use o formato do WhatsApp: *asteriscos* para destacar e • para listas. " +
            "Não comente estas instruções.");

        return instruction.ToString();
    }

    private static void AppendShifts(StringBuilder instruction, WorkSchedule schedule)
    {
        if (schedule.Status == WorkScheduleStatus.Unavailable)
        {
            instruction.AppendLine(UnknownShiftsLine);
        }
        else if (schedule.Shifts.Count == 0)
        {
            instruction.AppendLine(NoShiftsLine);
        }
        else
        {
            foreach (var shift in schedule.Shifts)
            {
                instruction.AppendLine(FormatShift(shift));
            }
        }
    }

    private static string FormatShift(WorkShift shift)
    {
        var title = string.IsNullOrWhiteSpace(shift.Title) ? "Turno" : shift.Title;
        var start = shift.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
        var end = shift.End.ToString("HH:mm", CultureInfo.InvariantCulture);
        var nextDay = shift.EndsNextDay ? " do dia seguinte" : string.Empty;

        return $"- {title}: das {start} até as {end}{nextDay}";
    }
}
