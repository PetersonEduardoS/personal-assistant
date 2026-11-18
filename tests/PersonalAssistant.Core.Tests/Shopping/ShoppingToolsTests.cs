using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Shopping;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Shopping;

public class ShoppingToolsTests
{
    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo", Household = "casa" };
    private static readonly UserProfile Karina = new() { PhoneNumber = "353800000002", Name = "Karina", Household = "casa" };
    private static readonly UserProfile Visitor = new() { PhoneNumber = "353800000003", Name = "Visita", Household = "outra" };
    private static readonly UserProfile Alone = new() { PhoneNumber = "353800000004", Name = "Sozinho" };

    private static readonly string[] ShoppingTools = ["add_shopping_items", "list_shopping", "mark_bought", "remove_shopping_item"];

    private readonly FakeShoppingListStore _store = new();

    [Fact]
    public void CreateFor_AddsTheShoppingTools_OnlyForUsersWithAHousehold()
    {
        Assert.All(ShoppingTools, name => Assert.Contains(name, ToolNames(Eduardo)));
        Assert.All(ShoppingTools, name => Assert.DoesNotContain(name, ToolNames(Alone)));
    }

    [Fact]
    public async Task Add_SavesSeveralItems_WithSectionQuantityAndWhoAdded()
    {
        var result = await Run(Eduardo, "add_shopping_items", """
            { "items": [
                { "name": "Leite", "section": "laticinios" },
                { "name": "Ovos", "quantity": "2 dúzias", "section": "laticinios" },
                { "name": "Sabão em pó", "section": "limpeza" } ] }
            """);

        Assert.Contains("Adicionados: Leite, Ovos (2 dúzias), Sabão em pó", result);
        Assert.Equal(3, _store.Items.Count);
        var eggs = _store.Items[1].Item;
        Assert.Equal("casa", _store.Items[1].Household);
        Assert.Equal("2 dúzias", eggs.Quantity);
        Assert.Equal("laticinios", eggs.Section);
        Assert.Equal("Eduardo", eggs.AddedBy);
    }

    [Fact]
    public async Task Add_DoesNotDuplicate_AnItemAlreadyOnTheList()
    {
        await _store.AddAsync("casa", "Leite", null, "laticinios", "Karina");

        var result = await Run(Eduardo, "add_shopping_items", """{ "items": [ { "name": "leite", "section": "laticinios" } ] }""");

        Assert.Contains("Já estavam na lista: Leite", result);
        Assert.Single(_store.Items);
    }

    [Fact]
    public async Task Add_PutsAnUnknownSection_InOthers()
    {
        await Run(Eduardo, "add_shopping_items", """{ "items": [ { "name": "Pilhas", "section": "eletronicos" } ] }""");

        Assert.Equal("outros", Assert.Single(_store.Items).Item.Section);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "items": [] }""")]
    [InlineData("""{ "items": [ { "name": "  ", "section": "outros" } ] }""")]
    public async Task Add_RejectsMissingItems(string input)
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "add_shopping_items", input));
        Assert.Empty(_store.Items);
    }

    [Fact]
    public async Task List_ShowsTheSharedList_GroupedBySection_InStoreOrder()
    {
        var soap = await _store.AddAsync("casa", "Sabão em pó", null, "limpeza", "Karina");
        var banana = await _store.AddAsync("casa", "Banana", "1 cacho", "hortifruti", "Eduardo");
        await _store.AddAsync("outra", "Cerveja", null, "bebidas", "Visita");

        var result = await Run(Eduardo, "list_shopping", "{}");

        Assert.Contains("2 itens", result);
        Assert.Contains($"[id {banana.Item.Id}] Banana (1 cacho), pedido por Eduardo", result);
        Assert.Contains($"[id {soap.Item.Id}] Sabão em pó, pedido por Karina", result);
        Assert.True(result.IndexOf("Hortifrúti", StringComparison.Ordinal) < result.IndexOf("Limpeza", StringComparison.Ordinal));
        Assert.DoesNotContain("Cerveja", result);
    }

    [Fact]
    public async Task List_SaysWhenTheListIsEmpty()
    {
        Assert.Contains("vazia", await Run(Karina, "list_shopping", "{}"));
    }

    [Fact]
    public async Task MarkBought_All_EmptiesTheList()
    {
        await _store.AddAsync("casa", "Leite", null, "laticinios", "Karina");
        await _store.AddAsync("casa", "Pão", null, "padaria", "Karina");

        var result = await Run(Eduardo, "mark_bought", """{ "all": true }""");

        Assert.Contains("2 itens", result);
        Assert.All(_store.Items, entry => Assert.True(entry.Bought));
    }

    [Fact]
    public async Task MarkBought_AllExcept_KeepsTheMissingItems()
    {
        await _store.AddAsync("casa", "Leite", null, "laticinios", "Karina");
        var coffee = await _store.AddAsync("casa", "Café", null, "mercearia", "Eduardo");

        var result = await Run(Eduardo, "mark_bought", $$"""{ "all": true, "except_item_ids": [{{coffee.Item.Id}}] }""");

        Assert.Contains("Ficou na lista: Café", result);
        Assert.False(_store.Items.Single(entry => entry.Item.Name == "Café").Bought);
        Assert.True(_store.Items.Single(entry => entry.Item.Name == "Leite").Bought);
    }

    [Fact]
    public async Task MarkBought_SomeItems()
    {
        var milk = await _store.AddAsync("casa", "Leite", null, "laticinios", "Karina");
        await _store.AddAsync("casa", "Café", null, "mercearia", "Eduardo");

        await Run(Karina, "mark_bought", $$"""{ "item_ids": [{{milk.Item.Id}}] }""");

        Assert.True(_store.Items.Single(entry => entry.Item.Name == "Leite").Bought);
        Assert.False(_store.Items.Single(entry => entry.Item.Name == "Café").Bought);
    }

    [Fact]
    public async Task MarkBought_RequiresAllOrIds()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "mark_bought", "{}"));
    }

    [Fact]
    public async Task Remove_TakesTheItemOffTheList()
    {
        var soda = await _store.AddAsync("casa", "Refrigerante", null, "bebidas", "Eduardo");

        var result = await Run(Karina, "remove_shopping_item", $$"""{ "item_id": {{soda.Item.Id}} }""");

        Assert.Contains("Refrigerante", result);
        Assert.Empty(_store.Items);
    }

    [Fact]
    public async Task Remove_CannotTouchAnotherHouseholdsList()
    {
        var beer = await _store.AddAsync("outra", "Cerveja", null, "bebidas", "Visita");

        await Assert.ThrowsAsync<AssistantToolException>(() =>
            Run(Eduardo, "remove_shopping_item", $$"""{ "item_id": {{beer.Item.Id}} }"""));
        Assert.Single(_store.Items);
    }

    private string[] ToolNames(UserProfile user) => CreateFactory().CreateFor(user).Select(tool => tool.Name).ToArray();

    private AssistantToolFactory CreateFactory() => new(
        new FakeAppointmentStore(),
        new FakeWorkScheduleProvider(),
        new FakeDeadlineProvider(),
        new FixedTimeProvider(DateTimeOffset.Parse("2026-11-18T10:00:00Z")),
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"),
        new FakeNoteStore(),
        new FakeEmailInbox(),
        new FakeMorningBriefPreferenceStore(),
        _store,
        TestHousehold.Notifier(), new FakeReceiptStore());

    private async Task<string> Run(UserProfile user, string name, string input)
    {
        using var document = JsonDocument.Parse(input);

        return await CreateFactory().CreateFor(user).Single(tool => tool.Name == name).ExecuteAsync(document.RootElement);
    }
}
