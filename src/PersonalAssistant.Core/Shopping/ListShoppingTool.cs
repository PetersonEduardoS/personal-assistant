using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

internal sealed class ListShoppingTool(IShoppingListStore store, UserProfile user) : IAssistantTool
{
    public string Name => "list_shopping";

    public string Description =>
        "Mostra a lista de compras da casa, separada por seção do mercado, com quem pediu cada item. " +
        "Use quando a pessoa pedir a lista. Mostre agrupado por seção, com • para os itens, sem os ids " +
        "(os ids servem só para mark_bought e remove_shopping_item).";

    public string InputSchema => """{ "type": "object", "properties": {} }""";

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var items = await store.ListPendingAsync(user.Household!, cancellationToken);

        return items.Count == 0 ? "A lista de compras da casa está vazia." : ShoppingText.FormatList(items);
    }
}
