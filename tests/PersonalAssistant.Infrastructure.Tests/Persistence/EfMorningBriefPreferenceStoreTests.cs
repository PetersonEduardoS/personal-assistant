using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class EfMorningBriefPreferenceStoreTests : IDisposable
{
    private const string Eduardo = "353800000001";
    private const string Karina = "353800000002";

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNothingWasChosen()
    {
        await using var context = _database.CreateContext();

        Assert.Null(await new EfMorningBriefPreferenceStore(context, _clock).GetAsync(Karina));
    }

    [Fact]
    public async Task SetAsync_SavesAndReplacesTheChoice_PerUser()
    {
        await using (var context = _database.CreateContext())
        {
            var store = new EfMorningBriefPreferenceStore(context, _clock);
            await store.SetAsync(Karina, MorningBriefPreference.At(new TimeOnly(8, 0)));
            await store.SetAsync(Karina, MorningBriefPreference.At(new TimeOnly(9, 30)));
            await store.SetAsync(Eduardo, MorningBriefPreference.Off);
        }

        await using (var context = _database.CreateContext())
        {
            var store = new EfMorningBriefPreferenceStore(context, _clock);
            Assert.Equal(MorningBriefPreference.At(new TimeOnly(9, 30)), await store.GetAsync(Karina));
            Assert.Equal(MorningBriefPreference.Off, await store.GetAsync(Eduardo));
        }
    }

    [Fact]
    public async Task ClearAsync_RemovesOnlyThatUsersChoice()
    {
        await using var context = _database.CreateContext();
        var store = new EfMorningBriefPreferenceStore(context, _clock);
        await store.SetAsync(Karina, MorningBriefPreference.At(new TimeOnly(8, 0)));
        await store.SetAsync(Eduardo, MorningBriefPreference.Off);

        await store.ClearAsync(Karina);

        Assert.Null(await store.GetAsync(Karina));
        Assert.Equal(MorningBriefPreference.Off, await store.GetAsync(Eduardo));
    }
}
