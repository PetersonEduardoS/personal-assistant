using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Shopping;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Shopping;

public class ShoppingPricesTests
{
    // Wednesday, 18 November 2026, 17:00 in Dublin.
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 17, 0, 0, TimeSpan.Zero);

    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo", Household = "casa" };

    private readonly FakeReceiptStore _receipts = new();
    private readonly FakeShoppingListStore _shopping = new();

    [Fact]
    public async Task List_ShowsTheKnownPrices_AndAsksForTheTotal()
    {
        await SaveNovemberReceipt();
        await _shopping.AddAsync("casa", "Carne moída", null, "carnes", "Eduardo");

        var result = await Run("list_shopping", "{}");

        Assert.Contains("Mince Beef 18% (Carne moída 18%): €5,89 por un, recibo de 15/11", result);
        Assert.Contains("Bananas (Banana): €1,82 por kg, recibo de 15/11", result);
        Assert.Contains("estimate_shopping_total", result);
    }

    [Fact]
    public async Task List_HasNoPriceSection_BeforeAnyReceipt()
    {
        await _shopping.AddAsync("casa", "Macarrão", null, "mercearia", "Eduardo");

        var result = await Run("list_shopping", "{}");

        Assert.DoesNotContain("Preços conhecidos", result);
        Assert.Contains("ainda não há recibos", result);
    }

    [Fact]
    public async Task Estimate_AddsKnownPricesAndEstimates_AndCountsItemsWithoutPrice()
    {
        await SaveNovemberReceipt();
        var mince = await _shopping.AddAsync("casa", "Carne moída", "2 pacotes", "carnes", "Eduardo");
        var bananas = await _shopping.AddAsync("casa", "Banana", null, "hortifruti", "Karina");
        var pasta = await _shopping.AddAsync("casa", "Macarrão", null, "mercearia", "Eduardo");
        await _shopping.AddAsync("casa", "Pilhas", null, "outros", "Eduardo");

        var result = await Run("estimate_shopping_total", $$"""
            { "items": [
                { "item_id": {{mince.Item.Id}}, "product": "mince beef 18%", "quantity": 2 },
                { "item_id": {{bananas.Item.Id}}, "product": "Bananas", "quantity": 1 },
                { "item_id": {{pasta.Item.Id}}, "estimated_price": 1.20 } ] }
            """);

        Assert.Contains("Carne moída (2 pacotes): €11,78 (2 x €5,89, recibo de 15/11)", result);
        Assert.Contains("Banana: €1,82 (1 kg x €1,82, recibo de 15/11)", result);
        Assert.Contains("Macarrão: ~€1,20 (estimado)", result);
        Assert.Contains("Pilhas: sem preço", result);
        Assert.Contains("Total aproximado: €14,80", result);
        Assert.Contains("2 com preço de recibo, 1 estimado, 1 sem preço", result);
    }

    [Fact]
    public async Task Estimate_TreatsAnUnknownProduct_AsWithoutPrice_UnlessEstimated()
    {
        var pasta = await _shopping.AddAsync("casa", "Macarrão", null, "mercearia", "Eduardo");

        var result = await Run("estimate_shopping_total", $$"""
            { "items": [ { "item_id": {{pasta.Item.Id}}, "product": "Spaghetti 500g" } ] }
            """);

        Assert.Contains("Macarrão: sem preço", result);
        Assert.Contains("Total aproximado: €0,00", result);
    }

    [Fact]
    public async Task Estimate_RejectsAnItemThatIsNotOnTheList()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run("estimate_shopping_total",
            """{ "items": [ { "item_id": 99, "estimated_price": 1 } ] }"""));
    }

    [Fact]
    public async Task Spending_SumsThisMonthsReceipts_ByDefault()
    {
        await SaveNovemberReceipt();
        await Save("Lidl Talbot Street", "2026-11-16", 4.27m, ("Plum Cherry Tomato", "Tomate cereja", 2m, 1.49m, 2.98m), ("Organic Beetroot 500g", "Beterraba", 1m, 1.29m, 1.29m));
        await Save("Lidl Drumcondra", "2026-10-26", 3.49m, ("Peri Peri Chicken Fillets", "Frango peri peri", 1m, 3.49m, 3.49m));

        var result = await Run("spending_summary", "{}");

        Assert.Contains("01/11 a 30/11", result);
        Assert.Contains("2 compras", result);
        Assert.Contains("Total: €11,98", result);
        Assert.Contains("15/11 Lidl Drumcondra: €7,71", result);
        Assert.DoesNotContain("26/10", result);
    }

    [Fact]
    public async Task Spending_UsesTheGivenPeriod_AndSaysWhenThereIsNothing()
    {
        var result = await Run("spending_summary", """{ "from_date": "2026-01-01", "to_date": "2026-01-31" }""");

        Assert.Contains("Nenhum recibo", result);
    }

    private Task SaveNovemberReceipt() => Save("Lidl Drumcondra", "2026-11-15", 7.71m,
        ("Mince Beef 18%", "Carne moída 18%", 1m, 5.89m, 5.89m),
        ("Bananas", "Banana", 1m, 1.82m, 1.82m, "kg"));

    private async Task Save(string store, string date, decimal total, params object[] lines)
    {
        var items = lines.Select(line => line switch
        {
            (string receiptName, string friendly, decimal quantity, decimal price, decimal lineTotal) =>
                new { receipt_name = receiptName, friendly_name = friendly, quantity, unit = "un", unit_price = price, line_total = lineTotal },
            (string receiptName, string friendly, decimal quantity, decimal price, decimal lineTotal, string unit) =>
                new { receipt_name = receiptName, friendly_name = friendly, quantity, unit, unit_price = price, line_total = lineTotal },
            _ => throw new ArgumentException("Bad test line")
        });

        await Run("save_receipt", JsonSerializer.Serialize(new { store, date, total, items }));
    }

    private AssistantToolFactory CreateFactory() => new(
        new FakeAppointmentStore(), new FakeWorkScheduleProvider(), new FakeDeadlineProvider(),
        new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"), new FakeNoteStore(),
        new FakeEmailInbox(), new FakeMorningBriefPreferenceStore(), _shopping, TestHousehold.Notifier(), _receipts);

    private async Task<string> Run(string name, string input)
    {
        using var document = JsonDocument.Parse(input);

        return await CreateFactory().CreateFor(Eduardo).Single(tool => tool.Name == name).ExecuteAsync(document.RootElement);
    }
}
