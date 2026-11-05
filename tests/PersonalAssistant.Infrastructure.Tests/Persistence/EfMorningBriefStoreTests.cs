using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class EfMorningBriefStoreTests : IDisposable
{
    private const string Eduardo = "353800000001";
    private const string Karina = "353800000002";

    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new(Now);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenThereIsNoBriefForThatDay()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        var brief = await store.GetAsync(Eduardo, Today);

        Assert.Null(brief);
    }

    [Fact]
    public async Task TryAddAsync_ReturnsTrue_AndStoresUndeliveredBrief()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        var added = await store.TryAddAsync(Eduardo, Today, "Bom dia, Eduardo!");

        Assert.True(added);
        var brief = await store.GetAsync(Eduardo, Today);
        Assert.Equal(new MorningBrief(Eduardo, Today, "Bom dia, Eduardo!", DeliveredAt: null), brief);
        Assert.False(brief!.IsDelivered);
    }

    [Fact]
    public async Task TryAddAsync_ReturnsFalse_AndKeepsFirstBrief_WhenThatDayAlreadyHasOne()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        await store.TryAddAsync(Eduardo, Today, "Primeiro resumo");

        var addedAgain = await store.TryAddAsync(Eduardo, Today, "Segundo resumo");

        Assert.False(addedAgain);
        var brief = await store.GetAsync(Eduardo, Today);
        Assert.Equal("Primeiro resumo", brief!.Content);
    }

    [Fact]
    public async Task TryAddAsync_AllowsBriefsForOtherUsersAndOtherDays()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        await store.TryAddAsync(Eduardo, Today, "Resumo do Eduardo");

        var addedForKarina = await store.TryAddAsync(Karina, Today, "Resumo da Karina");
        var addedForTomorrow = await store.TryAddAsync(Eduardo, Today.AddDays(1), "Resumo de amanhã");

        Assert.True(addedForKarina);
        Assert.True(addedForTomorrow);
    }

    [Fact]
    public async Task TryMarkDeliveredAsync_ReturnsTrue_AndRecordsDeliveryTime()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        await store.TryAddAsync(Eduardo, Today, "Bom dia, Eduardo!");
        _clock.Now = Now.AddMinutes(20);

        var marked = await store.TryMarkDeliveredAsync(Eduardo, Today);

        Assert.True(marked);
        var brief = await store.GetAsync(Eduardo, Today);
        Assert.True(brief!.IsDelivered);
        Assert.Equal(Now.AddMinutes(20), brief.DeliveredAt);
    }

    [Fact]
    public async Task TryMarkDeliveredAsync_ReturnsFalse_AndKeepsFirstDeliveryTime_WhenAlreadyDelivered()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        await store.TryAddAsync(Eduardo, Today, "Bom dia, Eduardo!");
        await store.TryMarkDeliveredAsync(Eduardo, Today);
        _clock.Now = Now.AddHours(1);

        var markedAgain = await store.TryMarkDeliveredAsync(Eduardo, Today);

        Assert.False(markedAgain);
        var brief = await store.GetAsync(Eduardo, Today);
        Assert.Equal(Now, brief!.DeliveredAt);
    }

    [Fact]
    public async Task TryMarkDeliveredAsync_ReturnsFalse_WhenThereIsNoBriefForThatDay()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        var marked = await store.TryMarkDeliveredAsync(Eduardo, Today);

        Assert.False(marked);
    }

    private EfMorningBriefStore CreateStore(AssistantDbContext context) => new(context, _clock);
}
