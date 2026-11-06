using PersonalAssistant.Core.Users;
using PersonalAssistant.Infrastructure.Users;

namespace PersonalAssistant.Infrastructure.Tests.Users;

public sealed class FileMorningBriefRoutineProviderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"routine-{Guid.NewGuid():N}.json");
    private readonly FileMorningBriefRoutineProvider _provider = new();
    public void Dispose() => File.Delete(_path);

    [Fact]
    public async Task GetAsync_NoConfigurationReturnsNull()
    {
        Assert.Null(await _provider.GetAsync(new UserProfile()));
    }

    [Fact]
    public async Task GetAsync_ConfiguredMissingFileThrows()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _provider.GetAsync(new() { MorningBriefRoutineFilePath = _path }));
    }

    [Fact]
    public async Task GetAsync_ReadsDatesTimesAndReloadsChanges()
    {
        await File.WriteAllTextAsync(_path, """
            { "LatestTime": "09:00:00", "PreparationMinutes": 30, "Appointments": [
              { "Name": "Class", "Date": "2026-11-17", "Start": "10:00:00", "TravelMinutes": 75 }
            ] }
            """);
        var user = new UserProfile { MorningBriefRoutineFilePath = _path };
        var routine = await _provider.GetAsync(user);
        Assert.Equal(new TimeOnly(8, 15), routine!.GetTime(new(2026, 11, 17), Core.WorkSchedules.WorkSchedule.NotConfigured));
        await File.WriteAllTextAsync(_path, """{ "LatestTime": "08:00:00" }""");
        Assert.Equal(new TimeOnly(8, 0), (await _provider.GetAsync(user))!.LatestTime);
    }

    [Fact]
    public async Task GetAsync_InvalidWeeklyAppointmentThrows()
    {
        await File.WriteAllTextAsync(_path, """{ "Appointments": [{ "DayOfWeek": "Thursday", "Start": "08:30:00" }] }""");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.GetAsync(new() { MorningBriefRoutineFilePath = _path }));
    }
}
