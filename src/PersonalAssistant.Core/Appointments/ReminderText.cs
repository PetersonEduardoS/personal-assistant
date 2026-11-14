namespace PersonalAssistant.Core.Appointments;

internal static class ReminderText
{
    /// <summary>Formats minutes as "30 minutos", "1 hora", "2 horas" or "1 hora e 30 minutos".</summary>
    public static string FormatDuration(int totalMinutes)
    {
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        var parts = new List<string>();

        if (hours > 0)
        {
            parts.Add(hours == 1 ? "1 hora" : $"{hours} horas");
        }

        if (minutes > 0 || hours == 0)
        {
            parts.Add(minutes == 1 ? "1 minuto" : $"{minutes} minutos");
        }

        return string.Join(" e ", parts);
    }

    public static string DescribeSetting(int? minutesBefore) =>
        minutesBefore is null ? "sem lembrete" : $"lembrete {FormatDuration(minutesBefore.Value)} antes";
}
