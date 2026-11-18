using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class EfShoppingListStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero));

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task AddAsync_StoresTheItem_AndListsItForTheHousehold()
    {
        await using var context = _database.CreateContext();
        var store = new EfShoppingListStore(context, _clock);

        var result = await store.AddAsync("casa", "  Ovos ", "2 dúzias", "laticinios", "Karina");

        Assert.True(result.WasAdded);
        var item = Assert.Single(await store.ListPendingAsync("casa"));
        Assert.Equal(result.Item, item);
        Assert.Equal("Ovos", item.Name);
        Assert.Equal("2 dúzias", item.Quantity);
        Assert.Equal("Karina", item.AddedBy);
        Assert.Equal(_clock.GetUtcNow(), item.AddedAt);
        Assert.Empty(await store.ListPendingAsync("outra"));
    }

    [Fact]
    public async Task AddAsync_ReturnsTheExistingItem_ForTheSameNameIgnoringCase()
    {
        await using var context = _database.CreateContext();
        var store = new EfShoppingListStore(context, _clock);
        var first = await store.AddAsync("casa", "Pão", null, "padaria", "Karina");

        var second = await store.AddAsync("casa", "PÃO", null, "padaria", "Eduardo");

        Assert.False(second.WasAdded);
        Assert.Equal(first.Item.Id, second.Item.Id);
        Assert.Single(await store.ListPendingAsync("casa"));
    }

    [Fact]
    public async Task MarkBoughtAsync_HidesTheBoughtItems_AndLetsThemBeAddedAgain()
    {
        await using var context = _database.CreateContext();
        var store = new EfShoppingListStore(context, _clock);
        var milk = await store.AddAsync("casa", "Leite", null, "laticinios", "Karina");
        await store.AddAsync("casa", "Café", null, "mercearia", "Eduardo");

        var marked = await store.MarkBoughtAsync("casa", [milk.Item.Id]);

        Assert.Equal(1, marked);
        Assert.Equal("Café", Assert.Single(await store.ListPendingAsync("casa")).Name);
        Assert.True((await store.AddAsync("casa", "Leite", null, "laticinios", "Karina")).WasAdded);
    }

    [Fact]
    public async Task MarkBoughtAsync_And_RemoveAsync_IgnoreAnotherHousehold()
    {
        await using var context = _database.CreateContext();
        var store = new EfShoppingListStore(context, _clock);
        var beer = await store.AddAsync("outra", "Cerveja", null, "bebidas", "Visita");

        Assert.Equal(0, await store.MarkBoughtAsync("casa", [beer.Item.Id]));
        Assert.Null(await store.RemoveAsync("casa", beer.Item.Id));
        Assert.Single(await store.ListPendingAsync("outra"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesThePendingItem()
    {
        await using var context = _database.CreateContext();
        var store = new EfShoppingListStore(context, _clock);
        var soda = await store.AddAsync("casa", "Refrigerante", null, "bebidas", "Eduardo");

        Assert.Equal(soda.Item, await store.RemoveAsync("casa", soda.Item.Id));
        Assert.Empty(await store.ListPendingAsync("casa"));
    }
}
