using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Shopping;

internal sealed class SpendingSummaryTool(
    IReceiptStore receipts,
    UserProfile user,
    TimeProvider clock,
    TimeZoneInfo timeZone) : IAssistantTool
{
    public string Name => "spending_summary";

    public string Description =>
        "Mostra quanto a casa gastou em compras num período, pelos recibos salvos: total, número de compras, " +
        "economia com ofertas e cada recibo. Use para \"quanto gastamos esse mês?\" ou \"e em outubro?\". " +
        "Sem datas, usa o mês atual. Só conta compras com recibo salvo; diga isso se fizer diferença.";

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "from_date": { "type": "string", "description": "Primeiro dia, AAAA-MM-DD" },
            "to_date": { "type": "string", "description": "Último dia (incluído), AAAA-MM-DD" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(AppointmentText.LocalNow(clock, timeZone));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var fromText = AppointmentText.GetOptionalString(input, "from_date");
        var toText = AppointmentText.GetOptionalString(input, "to_date");
        var fromDate = fromText is null ? monthStart : AppointmentText.ParseDate(fromText, "from_date");
        var toDate = toText is null ? fromDate.AddMonths(1).AddDays(-1) : AppointmentText.ParseDate(toText, "to_date");

        if (toDate < fromDate)
        {
            throw new AssistantToolException("O campo to_date não pode ser antes de from_date.");
        }

        var period = $"{Money.ShortDate(fromDate)} a {Money.ShortDate(toDate)}";
        var saved = await receipts.ListAsync(user.Household!, fromDate, toDate, cancellationToken);

        if (saved.Count == 0)
        {
            return $"Nenhum recibo salvo de {period}.";
        }

        var total = saved.Sum(receipt => receipt.Total);
        var savings = saved.Sum(receipt => receipt.Savings);
        var purchases = saved.Count == 1 ? "1 compra" : $"{saved.Count} compras";
        var result = new StringBuilder($"Gastos de {period}: {purchases}.\nTotal: {Money.Format(total)}");

        if (savings > 0)
        {
            result.Append($" (economia com ofertas: {Money.Format(savings)})");
        }

        result.Append($"\nMédia por compra: {Money.Format(Math.Round(total / saved.Count, 2))}\n");

        foreach (var receipt in saved)
        {
            result.Append($"\n- {Money.ShortDate(receipt.Date)} {receipt.Store}: {Money.Format(receipt.Total)}");
        }

        return result.ToString();
    }
}
