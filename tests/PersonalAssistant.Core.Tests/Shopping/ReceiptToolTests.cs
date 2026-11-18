using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Shopping;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Shopping;

public class ReceiptToolTests
{
    // Wednesday, 18 November 2026, 17:00 in Dublin.
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 17, 0, 0, TimeSpan.Zero);

    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo", Household = "casa" };
    private static readonly UserProfile Alone = new() { PhoneNumber = "353800000004", Name = "Sozinho" };

    // The receipt of 28/09/26 cut down: a weighed item, a quantity line, a discount and a deposit.
    private const string Receipt = """
        {
          "store": "Lidl Drumcondra",
          "date": "2026-09-28",
          "total": 23.88,
          "deposits": 0.25,
          "items": [
            { "receipt_name": "Bananas", "friendly_name": "Banana", "quantity": 0.926, "unit": "kg", "unit_price": 1.82, "line_total": 1.69 },
            { "receipt_name": "Mince Beef 18%", "friendly_name": "Carne moída 18%", "quantity": 2, "unit": "un", "unit_price": 5.89, "line_total": 11.78 },
            { "receipt_name": "20 Large Barn Eggs", "friendly_name": "Ovos (20)", "quantity": 1, "unit": "un", "unit_price": 5.35, "line_total": 5.35 },
            { "receipt_name": "Washing up Liquid Original", "friendly_name": "Detergente", "quantity": 1, "unit": "un", "unit_price": 1.99, "line_total": 1.99, "discount": 0.20 },
            { "receipt_name": "MiWadi Blackcurrant", "friendly_name": "Suco concentrado", "quantity": 1, "unit": "un", "unit_price": 3.02, "line_total": 3.02 }
          ]
        }
        """;

    private readonly FakeReceiptStore _receipts = new();
    private readonly FakeShoppingListStore _shopping = new();

    [Fact]
    public void CreateFor_AddsTheReceiptTool_OnlyForUsersWithAHousehold()
    {
        Assert.Contains("save_receipt", ToolNames(Eduardo));
        Assert.DoesNotContain("save_receipt", ToolNames(Alone));
    }

    [Fact]
    public void Description_ExplainsHowToReadALidlReceipt()
    {
        var description = Tool(Eduardo).Description;

        Assert.Contains("2 x 1.49", description);
        Assert.Contains("kg", description);
        Assert.Contains("Lidl Plus", description);
        Assert.Contains("Deposit", description);
    }

    [Fact]
    public async Task Save_StoresTheReceipt_WithItsLines()
    {
        var result = await Run(Receipt);

        var (household, saved) = Assert.Single(_receipts.Receipts);
        Assert.Equal("casa", household);
        Assert.Equal("Lidl Drumcondra", saved.Store);
        Assert.Equal(new DateOnly(2026, 9, 28), saved.Date);
        Assert.Equal(23.88m, saved.Total);
        Assert.Equal(0.20m, saved.Savings);
        Assert.Equal(0.25m, saved.Deposits);
        Assert.Equal("Eduardo", saved.AddedBy);
        Assert.Equal(5, saved.Lines.Count);
        var bananas = saved.Lines[0];
        Assert.Equal(("Banana", 0.926m, "kg", 1.82m), (bananas.FriendlyName, bananas.Quantity, bananas.Unit, bananas.UnitPrice));
        Assert.Contains("Recibo salvo: Lidl Drumcondra, 28/09, €23,88 (5 produtos)", result);
    }

    [Fact]
    public async Task Save_RefusesAReceiptWhoseLinesDoNotAddUpToTheTotal()
    {
        var misread = Receipt.Replace("\"total\": 23.88", "\"total\": 25.88");

        var exception = await Assert.ThrowsAsync<AssistantToolException>(() => Run(misread));

        Assert.Contains("23,88", exception.Message);
        Assert.Contains("25,88", exception.Message);
        Assert.Empty(_receipts.Receipts);
    }

    [Fact]
    public async Task Save_RefusesALineWhoseQuantityTimesPriceIsWrong()
    {
        var misread = Receipt.Replace("\"unit_price\": 5.89, \"line_total\": 11.78", "\"unit_price\": 5.39, \"line_total\": 11.78");

        var exception = await Assert.ThrowsAsync<AssistantToolException>(() => Run(misread));

        Assert.Contains("Mince Beef 18%", exception.Message);
    }

    [Theory]
    [InlineData("\"date\": \"2026-09-28\"", "\"date\": \"2026-12-01\"")]
    [InlineData("\"unit\": \"kg\"", "\"unit\": \"box\"")]
    [InlineData("\"items\": [", "\"items\": [], \"x\": [")]
    public async Task Save_RejectsInvalidData(string from, string to)
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Receipt.Replace(from, to)));
        Assert.Empty(_receipts.Receipts);
    }

    [Fact]
    public async Task Save_DoesNotSaveTheSameReceiptTwice()
    {
        await Run(Receipt);

        var result = await Run(Receipt);

        Assert.Contains("já estava salvo", result);
        Assert.Single(_receipts.Receipts);
    }

    [Fact]
    public async Task Save_MarksTheListItemsThatWereBought_AndSaysWhatIsLeft()
    {
        var eggs = await _shopping.AddAsync("casa", "Ovos", null, "laticinios", "Karina");
        await _shopping.AddAsync("casa", "Atum", null, "mercearia", "Eduardo");
        var input = Receipt.TrimEnd().TrimEnd('}') + $$""", "bought_item_ids": [{{eggs.Item.Id}}] }""";

        var result = await Run(input);

        Assert.True(_shopping.Items.Single(entry => entry.Item.Name == "Ovos").Bought);
        Assert.False(_shopping.Items.Single(entry => entry.Item.Name == "Atum").Bought);
        Assert.Contains("Ficou na lista: Atum", result);
    }

    private string[] ToolNames(UserProfile user) => CreateFactory().CreateFor(user).Select(tool => tool.Name).ToArray();

    private IAssistantTool Tool(UserProfile user) => CreateFactory().CreateFor(user).Single(tool => tool.Name == "save_receipt");

    private AssistantToolFactory CreateFactory() => new(
        new FakeAppointmentStore(), new FakeWorkScheduleProvider(), new FakeDeadlineProvider(),
        new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"), new FakeNoteStore(),
        new FakeEmailInbox(), new FakeMorningBriefPreferenceStore(), _shopping, TestHousehold.Notifier(), _receipts);

    private async Task<string> Run(string input)
    {
        using var document = JsonDocument.Parse(input);

        return await Tool(Eduardo).ExecuteAsync(document.RootElement);
    }
}
