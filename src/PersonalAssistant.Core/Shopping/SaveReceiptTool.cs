using System.Globalization;
using System.Text.Json;
using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

internal sealed class SaveReceiptTool(
    IReceiptStore receipts,
    IShoppingListStore shopping,
    UserProfile user,
    TimeProvider clock,
    TimeZoneInfo timeZone) : IAssistantTool
{
    /// <summary>A weighed item can differ by a cent or two from quantity times price.</summary>
    private const decimal LineTolerance = 0.02m;

    private const decimal TotalTolerance = 0.05m;

    private const int MaxItems = 80;

    public string Name => "save_receipt";

    public string Description =>
        "Salva um recibo de supermercado que a pessoa mandou em foto, para guardar os preços e o gasto da casa. " +
        "Leia o recibo com atenção, linha por linha. Cada produto vira um item: receipt_name como está no recibo, " +
        "friendly_name curto em português (o nome que vocês usariam na lista, por exemplo \"Carne moída 18%\" ou " +
        "\"Ovos (20)\"), quantity, unit, unit_price e line_total (o valor da linha). Quando houver uma linha como " +
        "\"2 x 1.49\" logo abaixo, quantity é 2 e unit_price é 1.49. Para produto pesado, como " +
        "\"0.926 kg x 1.82 EUR\", quantity é 0.926, unit é kg e unit_price é 1.82. unit_price é sempre o preço " +
        "normal; ofertas como \"Lidl Plus Offer(s) -0.70\" ou \"Savings -0.20\" vão em discount do item de cima, " +
        "como número positivo. Linhas \"Deposit\" (depósito de garrafa) não são produtos: some em deposits. " +
        "Ignore pagamento, troco, arredondamento, impostos (VAT), pontos e dados do cartão. total é o TOTAL do " +
        "recibo e date é a data impressa nele. Se a pessoa estava com a lista de compras, ponha em bought_item_ids " +
        "os ids dos itens da lista que aparecem no recibo (use list_shopping para ver os ids). " +
        "Se a soma não bater com o TOTAL, releia a foto e corrija antes de tentar de novo.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "store": { "type": "string", "description": "Loja, por exemplo Lidl Drumcondra" },
            "date": { "type": "string", "description": "Data do recibo, AAAA-MM-DD" },
            "total": { "type": "number", "description": "O TOTAL do recibo" },
            "deposits": { "type": "number", "description": "Soma das linhas Deposit, se houver" },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "receipt_name": { "type": "string" },
                  "friendly_name": { "type": "string" },
                  "quantity": { "type": "number" },
                  "unit": { "type": "string", "enum": ["un", "kg"] },
                  "unit_price": { "type": "number" },
                  "line_total": { "type": "number" },
                  "discount": { "type": "number" }
                },
                "required": ["receipt_name", "friendly_name", "quantity", "unit", "unit_price", "line_total"]
              }
            },
            "bought_item_ids": { "type": "array", "items": { "type": "integer" } }
          },
          "required": ["store", "date", "total", "items"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var store = ReadString(input, "store") ?? throw new AssistantToolException("Falta o campo store.");
        var date = AppointmentText.ParseDate(ReadString(input, "date") ?? string.Empty, "date");

        if (date > DateOnly.FromDateTime(AppointmentText.LocalNow(clock, timeZone)))
        {
            throw new AssistantToolException("A data do recibo está no futuro. Releia a data impressa no recibo.");
        }

        var total = ReadDecimal(input, "total") ?? throw new AssistantToolException("Falta o campo total.");
        var deposits = ReadDecimal(input, "deposits") ?? 0;
        var lines = ReadLines(input);

        // The receipt must add up: a misread photo is caught here instead of saving wrong prices.
        var sum = lines.Sum(line => line.LineTotal - line.Discount) + deposits;

        if (Math.Abs(sum - total) > TotalTolerance)
        {
            throw new AssistantToolException(
                $"A soma das linhas (menos descontos, mais depósitos) dá {Money.Format(sum)}, mas o TOTAL é " +
                $"{Money.Format(total)}. Releia a foto, confira cada linha e tente de novo.");
        }

        var result = await receipts.AddAsync(
            user.Household!,
            new NewReceipt(store, date, total, lines.Sum(line => line.Discount), deposits, user.Name, lines),
            cancellationToken);

        var dateText = date.ToString("dd/MM", CultureInfo.InvariantCulture);

        if (!result.WasAdded)
        {
            return $"Esse recibo ({store}, {dateText}, {Money.Format(total)}) já estava salvo. Nada mudou.";
        }

        var message = $"Recibo salvo: {store}, {dateText}, {Money.Format(total)} ({lines.Count} produtos).";

        var boughtIds = ShoppingText.ReadIds(input, "bought_item_ids");

        if (boughtIds.Count == 0)
        {
            return message;
        }

        var marked = await shopping.MarkBoughtAsync(user.Household!, boughtIds, cancellationToken);
        var left = (await shopping.ListPendingAsync(user.Household!, cancellationToken)).Select(ShoppingText.Describe).ToList();

        return $"{message} Marcados como comprados na lista: {ShoppingText.Count(marked)}. " +
            (left.Count == 0 ? "A lista ficou vazia." : $"Ficou na lista: {string.Join(", ", left)}.");
    }

    private static List<ReceiptLine> ReadLines(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array
            || items.GetArrayLength() == 0)
        {
            throw new AssistantToolException("Falta o campo items com os produtos do recibo.");
        }

        if (items.GetArrayLength() > MaxItems)
        {
            throw new AssistantToolException($"O recibo pode ter no máximo {MaxItems} produtos.");
        }

        return items.EnumerateArray().Select(ReadLine).ToList();
    }

    private static ReceiptLine ReadLine(JsonElement item)
    {
        var receiptName = ReadString(item, "receipt_name");
        var friendlyName = ReadString(item, "friendly_name");
        var unit = ReadString(item, "unit");
        var quantity = ReadDecimal(item, "quantity");
        var unitPrice = ReadDecimal(item, "unit_price");
        var lineTotal = ReadDecimal(item, "line_total");
        var discount = ReadDecimal(item, "discount") ?? 0;

        if (receiptName is null || friendlyName is null || quantity is not > 0 || unitPrice is not >= 0
            || lineTotal is not >= 0 || discount < 0 || unit is not (ReceiptLine.Each or ReceiptLine.Kilogram))
        {
            throw new AssistantToolException(
                "Cada item precisa de receipt_name, friendly_name, quantity maior que zero, unit (un ou kg), " +
                "unit_price e line_total sem valores negativos.");
        }

        if (Math.Abs(quantity.Value * unitPrice.Value - lineTotal.Value) > LineTolerance)
        {
            throw new AssistantToolException(
                $"No item {receiptName}, quantity vezes unit_price não dá line_total. Releia essa linha do recibo.");
        }

        return new ReceiptLine(receiptName, friendlyName, quantity.Value, unit, unitPrice.Value, lineTotal.Value, discount);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static decimal? ReadDecimal(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var number)
            ? number
            : null;
}
