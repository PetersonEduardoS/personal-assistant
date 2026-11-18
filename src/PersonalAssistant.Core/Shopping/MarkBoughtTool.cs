using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

internal sealed class MarkBoughtTool(IShoppingListStore store, UserProfile user) : IAssistantTool
{
    public string Name => "mark_bought";

    public string Description =>
        "Marca itens da lista de compras como comprados, tirando-os da lista. " +
        "\"Comprei tudo\": all true. \"Comprei tudo menos o café\": all true e except_item_ids com o id do café. " +
        "\"Comprei o leite\": item_ids. Use list_shopping antes para achar os ids; nunca chute. " +
        "Depois, diga o que ficou na lista para a próxima compra.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "all": { "type": "boolean", "description": "true para marcar a lista inteira" },
            "except_item_ids": { "type": "array", "items": { "type": "integer" }, "description": "Com all, os itens que não foram comprados" },
            "item_ids": { "type": "array", "items": { "type": "integer" }, "description": "Os itens comprados, quando não for a lista inteira" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var all = input.ValueKind == JsonValueKind.Object
            && input.TryGetProperty("all", out var value)
            && value.ValueKind == JsonValueKind.True;
        var itemIds = ShoppingText.ReadIds(input, "item_ids");
        var exceptIds = ShoppingText.ReadIds(input, "except_item_ids");

        if (!all && itemIds.Count == 0)
        {
            throw new AssistantToolException("Informe all true ou item_ids com os itens comprados.");
        }

        var pending = await store.ListPendingAsync(user.Household!, cancellationToken);
        var bought = all
            ? pending.Where(item => !exceptIds.Contains(item.Id)).Select(item => item.Id).ToList()
            : itemIds;

        var marked = await store.MarkBoughtAsync(user.Household!, bought, cancellationToken);
        var left = pending.Where(item => !bought.Contains(item.Id)).Select(ShoppingText.Describe).ToList();

        return $"Marcados como comprados: {ShoppingText.Count(marked)}. " +
            (left.Count == 0 ? "A lista ficou vazia." : $"Ficou na lista: {string.Join(", ", left)}.");
    }
}
