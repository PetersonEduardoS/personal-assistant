using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

internal sealed class RemoveShoppingItemTool(IShoppingListStore store, UserProfile user) : IAssistantTool
{
    public string Name => "remove_shopping_item";

    public string Description =>
        "Tira um item da lista de compras sem marcar como comprado, por exemplo quando a pessoa desistiu dele. " +
        "Use list_shopping antes para achar o id; nunca chute.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": { "item_id": { "type": "integer" } },
          "required": ["item_id"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("item_id", out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var itemId))
        {
            throw new AssistantToolException("Falta o campo item_id, que deve ser um número.");
        }

        var removed = await store.RemoveAsync(user.Household!, itemId, cancellationToken)
            ?? throw new AssistantToolException(
                $"Não existe item com o id {itemId} na lista. Use list_shopping para ver os ids.");

        return $"Tirado da lista: {ShoppingText.Describe(removed)}.";
    }
}
