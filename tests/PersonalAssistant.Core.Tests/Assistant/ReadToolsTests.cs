using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Deadlines;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Core.Tests.Assistant;

public class ReadToolsTests
{
    // Saturday, 14 November 2026, 02:00 in Dublin.
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Saturday = new(2026, 11, 14);

    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo" };

    private readonly FakeWorkScheduleProvider _schedules = new();
    private FakeDeadlineProvider _deadlines = new();

    [Fact]
    public async Task ListWorkShifts_ShowsEachDay_IncludingShiftsThatEndTheNextDay()
    {
        _schedules.Days[Saturday] = WorkSchedule.WithShifts([new(new(17, 0), new(1, 0), "Bar")]);
        _schedules.Days[Saturday.AddDays(1)] = WorkSchedule.WithShifts([new(new(13, 30), new(19, 30), "Bar")]);

        var result = await Run("list_work_shifts", """{ "from_date": "2026-11-14", "to_date": "2026-11-16" }""");

        Assert.Contains("- sábado, 14 de novembro: Bar das 17:00 até as 01:00 do dia seguinte", result);
        Assert.Contains("- domingo, 15 de novembro: Bar das 13:30 até as 19:30", result);
        Assert.Contains("- segunda, 16 de novembro: sem turno", result);
    }

    [Fact]
    public async Task ListWorkShifts_AlwaysWarnsThatADayWithoutShiftsMayNotBePublishedYet()
    {
        var result = await Run("list_work_shifts", "{}");

        Assert.Contains("escala ainda não publicada", result);
    }

    [Fact]
    public async Task ListWorkShifts_UsesTheNextSevenDays_ByDefault()
    {
        await Run("list_work_shifts", "{}");

        Assert.Equal(Enumerable.Range(0, 7).Select(day => Saturday.AddDays(day)), _schedules.RequestedDates);
    }

    [Theory]
    [InlineData("2026-11-14", "2026-11-28")]
    [InlineData("2026-11-20", "2026-11-14")]
    public async Task ListWorkShifts_RejectsInvalidPeriods(string from, string to)
    {
        await Assert.ThrowsAsync<AssistantToolException>(() =>
            Run("list_work_shifts", $$"""{ "from_date": "{{from}}", "to_date": "{{to}}" }"""));
    }

    [Fact]
    public async Task ListWorkShifts_SaysWhenTheScheduleCannotBeRead()
    {
        _schedules.Always = WorkSchedule.Unavailable;

        var result = await Run("list_work_shifts", "{}");

        Assert.Contains("não pôde ser lida", result);
    }

    [Fact]
    public async Task ListWorkShifts_SaysWhenThePersonHasNoSchedule()
    {
        _schedules.Always = WorkSchedule.NotConfigured;

        var result = await Run("list_work_shifts", "{}");

        Assert.Contains("não tem escala", result);
    }

    [Fact]
    public async Task ListDeadlines_ShowsTitleCourseDateAndTime()
    {
        _deadlines = new FakeDeadlineProvider(UpcomingDeadlines.WithDeadlines(
        [
            new("Integrated CA2 (50%) is due", "Machine Learning for Business",
                new DateTimeOffset(2026, 11, 27, 23, 59, 0, TimeSpan.Zero))
        ]));

        var result = await Run("list_deadlines", "{}");

        Assert.Contains("- Integrated CA2 (50%) is due (Machine Learning for Business): sexta, 27 de novembro, às 23:59", result);
        Assert.Equal(new[] { Saturday }, _deadlines.RequestedDates);
    }

    [Fact]
    public async Task ListDeadlines_SaysWhenThereAreNone()
    {
        _deadlines = new FakeDeadlineProvider(UpcomingDeadlines.WithDeadlines([]));

        var result = await Run("list_deadlines", "{}");

        Assert.Contains("Nenhum prazo", result);
    }

    [Fact]
    public async Task ListDeadlines_SaysWhenTheCalendarCannotBeRead()
    {
        _deadlines = new FakeDeadlineProvider(UpcomingDeadlines.Unavailable);

        var result = await Run("list_deadlines", "{}");

        Assert.Contains("não puderam ser lidos", result);
    }

    [Fact]
    public async Task ListDeadlines_SaysWhenThePersonHasNoCalendar()
    {
        var result = await Run("list_deadlines", "{}");

        Assert.Contains("não tem calendário", result);
    }

    private async Task<string> Run(string name, string input)
    {
        var factory = new AssistantToolFactory(
            new FakeAppointmentStore(),
            _schedules,
            _deadlines,
            new FixedTimeProvider(Now),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"),
            new FakeNoteStore(),
            new FakeEmailInbox(), new FakeMorningBriefPreferenceStore(), new FakeShoppingListStore(), TestHousehold.Notifier(), new FakeReceiptStore());
        var tool = factory.CreateFor(Eduardo).Single(candidate => candidate.Name == name);

        using var document = JsonDocument.Parse(input);

        return await tool.ExecuteAsync(document.RootElement);
    }
}
