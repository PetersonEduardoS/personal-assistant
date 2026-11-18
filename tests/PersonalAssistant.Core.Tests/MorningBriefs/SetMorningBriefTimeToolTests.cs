using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.MorningBriefs;

public class SetMorningBriefTimeToolTests
{
    private static readonly UserProfile Karina = new() { PhoneNumber = "353800000002", Name = "Karina" };

    private readonly FakeMorningBriefPreferenceStore _preferences = new();

    [Fact]
    public async Task Time_SavesTheChosenTime_AndConfirms()
    {
        var result = await Run("""{ "mode": "time", "time": "08:15" }""");

        Assert.Equal(MorningBriefPreference.At(new TimeOnly(8, 15)), _preferences.Preferences[Karina.PhoneNumber]);
        Assert.Contains("08:15", result);
    }

    [Theory]
    [InlineData("""{ "mode": "time" }""")]
    [InlineData("""{ "mode": "time", "time": "8h" }""")]
    [InlineData("""{ "mode": "time", "time": "04:59" }""")]
    [InlineData("""{ "mode": "time", "time": "12:01" }""")]
    [InlineData("""{ "mode": "later" }""")]
    [InlineData("""{ }""")]
    public async Task RejectsAnInvalidRequest(string input)
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(input));
        Assert.Empty(_preferences.Preferences);
    }

    [Fact]
    public async Task Off_TurnsTheBriefOff()
    {
        var result = await Run("""{ "mode": "off" }""");

        Assert.Equal(MorningBriefPreference.Off, _preferences.Preferences[Karina.PhoneNumber]);
        Assert.Contains("desligado", result);
    }

    [Fact]
    public async Task Automatic_RemovesTheChatChoice()
    {
        _preferences.Preferences[Karina.PhoneNumber] = MorningBriefPreference.At(new TimeOnly(8, 0));

        var result = await Run("""{ "mode": "automatic" }""");

        Assert.Empty(_preferences.Preferences);
        Assert.Contains("padrão", result);
    }

    private async Task<string> Run(string input)
    {
        var tool = new AssistantToolFactory(
                new FakeAppointmentStore(),
                new FakeWorkScheduleProvider(),
                new FakeDeadlineProvider(),
                new FixedTimeProvider(DateTimeOffset.Parse("2026-11-14T10:00:00Z")),
                TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"),
                new FakeNoteStore(),
                new FakeEmailInbox(),
                _preferences,
                new FakeShoppingListStore())
            .CreateFor(Karina)
            .Single(candidate => candidate.Name == "set_morning_brief_time");

        using var document = JsonDocument.Parse(input);

        return await tool.ExecuteAsync(document.RootElement);
    }
}
