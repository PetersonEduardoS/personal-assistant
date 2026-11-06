namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>
/// Writes dates in Brazilian Portuguese without depending on the server's culture data,
/// which some Docker images do not include.
/// </summary>
public static class PortugueseDates
{
    private static readonly string[] ShortWeekdays =
        ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];

    private static readonly string[] LongWeekdays =
        ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

    private static readonly string[] Months =
    [
        "janeiro", "fevereiro", "março", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    ];

    /// <summary>
    /// For example "quinta, 5 de novembro".
    /// </summary>
    public static string FormatShort(DateOnly date) =>
        $"{ShortWeekdays[(int)date.DayOfWeek]}, {date.Day} de {Months[date.Month - 1]}";

    /// <summary>
    /// For example "quinta-feira, 5 de novembro de 2026".
    /// </summary>
    public static string FormatLong(DateOnly date) =>
        $"{LongWeekdays[(int)date.DayOfWeek]}, {date.Day} de {Months[date.Month - 1]} de {date.Year}";
}
