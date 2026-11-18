using PersonalAssistant.Core.Shopping;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class EfReceiptStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 18, 17, 0, 0, TimeSpan.Zero));

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task AddAsync_StoresTheReceiptAndItsLines()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await new EfReceiptStore(context, _clock).AddAsync("casa", September());
            Assert.True(result.WasAdded);
        }

        await using (var context = _database.CreateContext())
        {
            var saved = Assert.Single(await new EfReceiptStore(context, _clock).ListAsync("casa", new(2026, 9, 1), new(2026, 9, 30)));
            Assert.Equal(("Lidl Drumcondra", 7.04m, 0.20m, 0.25m, "Eduardo"), (saved.Store, saved.Total, saved.Savings, saved.Deposits, saved.AddedBy));
            Assert.Equal(new[] { "Bananas", "Mince Beef 18%" }, saved.Lines.Select(line => line.ReceiptName).ToArray());
            Assert.Equal(0.926m, saved.Lines[0].Quantity);
        }
    }

    [Fact]
    public async Task AddAsync_DoesNotStoreTheSameReceiptTwice()
    {
        await using var context = _database.CreateContext();
        var store = new EfReceiptStore(context, _clock);
        var first = await store.AddAsync("casa", September());

        var second = await store.AddAsync("casa", September());

        Assert.False(second.WasAdded);
        Assert.Equal(first.Receipt.Id, second.Receipt.Id);
    }

    [Fact]
    public async Task ListLatestPricesAsync_KeepsTheMostRecentPricePerProduct_ForTheHousehold()
    {
        await using var context = _database.CreateContext();
        var store = new EfReceiptStore(context, _clock);
        await store.AddAsync("casa", September());
        await store.AddAsync("casa", new NewReceipt("Lidl Drumcondra", new DateOnly(2026, 6, 7), 5.99m, 0, 0, "Karina",
            [new ReceiptLine("MINCE BEEF 18%", "Carne moída 18%", 1, "un", 5.99m, 5.99m, 0)]));
        await store.AddAsync("outra", new NewReceipt("Lidl Talbot", new DateOnly(2026, 11, 1), 9.99m, 0, 0, "Visita",
            [new ReceiptLine("Mince Beef 18%", "Carne", 1, "un", 9.99m, 9.99m, 0)]));

        var prices = await store.ListLatestPricesAsync("casa");

        var mince = Assert.Single(prices, price => price.ReceiptName.Equals("Mince Beef 18%", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(5.89m, mince.UnitPrice);
        Assert.Equal(new DateOnly(2026, 9, 28), mince.Date);
        Assert.Equal(2, prices.Count);
    }

    private static NewReceipt September() => new(
        "Lidl Drumcondra", new DateOnly(2026, 9, 28), 7.04m, 0.20m, 0.25m, "Eduardo",
        [
            new ReceiptLine("Bananas", "Banana", 0.926m, "kg", 1.82m, 1.69m, 0.20m),
            new ReceiptLine("Mince Beef 18%", "Carne moída 18%", 1, "un", 5.89m, 5.89m, 0)
        ]);
}
