using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

internal sealed class AddShoppingItemsTool(IShoppingListStore store, UserProfile user) : IAssistantTool
{
    private const int MaxItems = 30;

    public string Name => "add_shopping_items";

    public string Description =>
        "Adiciona um ou mais itens na lista de compras da casa, que é compartilhada com quem mora junto. " +
        "Use para pedidos como \"põe leite e ovos na lista\". Escreva cada item com letra maiúscula no início, " +
        "no singular quando fizer sentido, e só ponha quantity se a pessoa disser. Escolha a seção do mercado " +
        "de cada item. Itens que já estão na lista não são repetidos. Depois, confirme em uma frase curta.";

    public string InputSchema => $$"""
        {
          "type": "object",
          "properties": {
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string", "description": "O item, por exemplo Leite" },
                  "quantity": { "type": "string", "description": "Opcional, por exemplo 2 caixas" },
                  "section": { "type": "string", "enum": {{JsonSerializer.Serialize(ShoppingSections.Keys)}} }
                },
                "required": ["name", "section"]
              }
            }
          },
          "required": ["items"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var items = ReadItems(input);
        var added = new List<string>();
        var existing = new List<string>();

        foreach (var (name, quantity, section) in items)
        {
            var result = await store.AddAsync(user.Household!, name, quantity, section, user.Name, cancellationToken);
            (result.WasAdded ? added : existing).Add(ShoppingText.Describe(result.Item));
        }

        var parts = new List<string>();

        if (added.Count > 0)
        {
            parts.Add($"Adicionados: {string.Join(", ", added)}.");
        }

        if (existing.Count > 0)
        {
            parts.Add($"Já estavam na lista: {string.Join(", ", existing)}.");
        }

        return string.Join(" ", parts);
    }

    private static List<(string Name, string? Quantity, string Section)> ReadItems(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array
            || items.GetArrayLength() == 0)
        {
            throw new AssistantToolException("Falta o campo items com pelo menos um item.");
        }

        if (items.GetArrayLength() > MaxItems)
        {
            throw new AssistantToolException($"Mande no máximo {MaxItems} itens de cada vez.");
        }

        return items.EnumerateArray().Select(item =>
        {
            var name = Read(item, "name");
            var quantity = Read(item, "quantity");

            if (string.IsNullOrEmpty(name))
            {
                throw new AssistantToolException("Todo item precisa de um name.");
            }

            if (name.Length > ShoppingItem.MaxNameLength || quantity?.Length > ShoppingItem.MaxQuantityLength)
            {
                throw new AssistantToolException(
                    $"O nome pode ter até {ShoppingItem.MaxNameLength} caracteres e a quantidade até " +
                    $"{ShoppingItem.MaxQuantityLength}.");
            }

            return (name, string.IsNullOrEmpty(quantity) ? null : quantity, ShoppingSections.Normalize(Read(item, "section")));
        }).ToList();
    }

    private static string? Read(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
}
