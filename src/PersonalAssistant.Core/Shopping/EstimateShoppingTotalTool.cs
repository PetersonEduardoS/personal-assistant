using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

/// <summary>
/// The assistant matches each list item to a product from the receipts; this tool does the arithmetic,
/// because a language model is not reliable with sums.
/// </summary>
internal sealed class EstimateShoppingTotalTool(IShoppingListStore store, IReceiptStore receipts, UserProfile user)
    : IAssistantTool
{
    public string Name => "estimate_shopping_total";

    public string Description =>
        "Calcula o valor de cada item da lista de compras e o total aproximado. Para cada item, informe item_id, " +
        "product com o nome exato do recibo (como aparece em list_shopping) e quantity: o número de unidades, " +
        "ou os kg para produtos vendidos por kg (estime o peso, por exemplo 1 kg de banana). Se nenhum produto " +
        "dos recibos servir, mande estimated_price com o seu palpite do preço no Lidl da Irlanda, ou deixe o item de " +
        "fora. Mostre o resultado por seção e diga claramente que o total é aproximado e quais valores são estimados.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "item_id": { "type": "integer" },
                  "product": { "type": "string", "description": "Nome do produto como está no recibo" },
                  "quantity": { "type": "number", "description": "Unidades ou kg; padrão 1" },
                  "estimated_price": { "type": "number", "description": "Palpite de preço total do item, quando não há produto" }
                },
                "required": ["item_id"]
              }
            }
          },
          "required": ["items"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var pending = await store.ListPendingAsync(user.Household!, cancellationToken);
        var prices = (await receipts.ListLatestPricesAsync(user.Household!, cancellationToken))
            .GroupBy(price => price.ReceiptName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var choices = ReadChoices(input);

        foreach (var itemId in choices.Keys.Where(id => pending.All(item => item.Id != id)))
        {
            throw new AssistantToolException($"O item {itemId} não está na lista. Use list_shopping para ver os ids.");
        }

        var total = 0m;
        var priced = 0;
        var estimated = 0;
        var unknown = 0;
        var result = new StringBuilder($"Lista com valores, {ShoppingText.Count(pending.Count)}:");

        foreach (var section in pending.GroupBy(item => item.Section).OrderBy(group => ShoppingSections.Order(group.Key)))
        {
            result.Append($"\n\n{ShoppingSections.DisplayName(section.Key)}:");

            foreach (var item in section)
            {
                var name = ShoppingText.Describe(item);
                choices.TryGetValue(item.Id, out var choice);

                if (choice.Product is not null && prices.TryGetValue(choice.Product.Trim(), out var price))
                {
                    var value = Math.Round(price.UnitPrice * choice.Quantity, 2);
                    var unit = price.Unit == ReceiptLine.Kilogram ? " kg" : string.Empty;
                    total += value;
                    priced++;
                    result.Append(
                        $"\n- {name}: {Money.Format(value)} ({Money.Quantity(choice.Quantity)}{unit} x " +
                        $"{Money.Format(price.UnitPrice)}, recibo de {Money.ShortDate(price.Date)})");
                }
                else if (choice.EstimatedPrice is { } guess)
                {
                    total += guess;
                    estimated++;
                    result.Append($"\n- {name}: ~{Money.Format(guess)} (estimado)");
                }
                else
                {
                    unknown++;
                    result.Append($"\n- {name}: sem preço");
                }
            }
        }

        result.Append(
            $"\n\nTotal aproximado: {Money.Format(total)} ({priced} com preço de recibo, " +
            $"{estimated} {(estimated == 1 ? "estimado" : "estimados")}, {unknown} sem preço).");

        return result.ToString();
    }

    private static Dictionary<long, (string? Product, decimal Quantity, decimal? EstimatedPrice)> ReadChoices(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            throw new AssistantToolException("Falta o campo items.");
        }

        var choices = new Dictionary<long, (string?, decimal, decimal?)>();

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("item_id", out var idValue)
                || !idValue.TryGetInt64(out var itemId))
            {
                throw new AssistantToolException("Todo item precisa de item_id numérico.");
            }

            var product = item.TryGetProperty("product", out var productValue) && productValue.ValueKind == JsonValueKind.String
                ? productValue.GetString()
                : null;
            var quantity = item.TryGetProperty("quantity", out var quantityValue)
                && quantityValue.ValueKind == JsonValueKind.Number
                && quantityValue.TryGetDecimal(out var number)
                && number > 0
                    ? number
                    : 1m;
            decimal? estimate = item.TryGetProperty("estimated_price", out var estimateValue)
                && estimateValue.ValueKind == JsonValueKind.Number
                && estimateValue.TryGetDecimal(out var guess)
                && guess >= 0
                    ? guess
                    : null;

            choices[itemId] = (product, quantity, estimate);
        }

        return choices;
    }
}
