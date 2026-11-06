using PersonalAssistant.Core.MorningBriefs;

namespace PersonalAssistant.Core.Tests.MorningBriefs;

public class PortugueseDatesTests
{
    [Theory]
    [InlineData(2026, 11, 5, "quinta, 5 de novembro")]
    [InlineData(2026, 11, 7, "sábado, 7 de novembro")]
    [InlineData(2026, 11, 8, "domingo, 8 de novembro")]
    [InlineData(2026, 12, 1, "terça, 1 de dezembro")]
    [InlineData(2027, 3, 3, "quarta, 3 de março")]
    public void FormatShort_ReturnsWeekdayDayAndMonth(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, PortugueseDates.FormatShort(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 11, 5, "quinta-feira, 5 de novembro de 2026")]
    [InlineData(2026, 11, 9, "segunda-feira, 9 de novembro de 2026")]
    [InlineData(2026, 11, 7, "sábado, 7 de novembro de 2026")]
    [InlineData(2027, 1, 10, "domingo, 10 de janeiro de 2027")]
    public void FormatLong_ReturnsFullWeekdayDayMonthAndYear(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, PortugueseDates.FormatLong(new DateOnly(year, month, day)));
    }
}
