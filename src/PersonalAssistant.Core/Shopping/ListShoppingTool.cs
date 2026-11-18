using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

internal sealed class ListShoppingTool(IShoppingListStore store, IReceiptStore receipts, UserProfile user) : IAssistantTool
{
    private const int MaxPrices = 150;

    public string Name => "list_shopping";

    public string Description =>
        "Mostra a lista de compras da casa, separada por seção do mercado, com quem pediu cada item, e os preços " +
        "conhecidos dos recibos. Use quando a pessoa pedir a lista. Se houver preços conhecidos, chame em seguida " +
        "estimate_shopping_total para mostrar o valor de cada item e o total aproximado no fim. Mostre agrupado por " +
        "seção, com • para os itens, sem os ids (os ids servem só para as outras ferramentas da lista).";

    public string InputSchema => """{ "type": "object", "properties": {} }""";

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var items = await store.ListPendingAsync(user.Household!, cancellationToken);

        if (items.Count == 0)
        {
            return "A lista de compras da casa está vazia.";
        }

        var result = new StringBuilder(ShoppingText.FormatList(items));
        var prices = await receipts.ListLatestPricesAsync(user.Household!, cancellationToken);

        if (prices.Count == 0)
        {
            result.Append("\n\nPor enquanto ainda não há recibos salvos, então não há preços para calcular o total.");
            return result.ToString();
        }

        result.Append(
            "\n\nPreços conhecidos (o recibo mais recente de cada produto). Ligue cada item da lista ao produto " +
            "certo e chame estimate_shopping_total:");

        foreach (var price in prices.Take(MaxPrices))
        {
            result.Append(
                $"\n- {price.ReceiptName} ({price.FriendlyName}): {Money.Format(price.UnitPrice)} por {price.Unit}, " +
                $"recibo de {Money.ShortDate(price.Date)}");
        }

        return result.ToString();
    }
}
