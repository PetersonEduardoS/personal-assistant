using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.Shopping;

namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>
/// SQLite keeps decimals as text, so sums and comparisons of money happen in memory; a household has few receipts.
/// </summary>
public sealed class EfReceiptStore(AssistantDbContext context, TimeProvider timeProvider) : IReceiptStore
{
    public async Task<ReceiptSaveResult> AddAsync(string household, NewReceipt receipt, CancellationToken cancellationToken = default)
    {
        var sameDay = await context.Receipts
            .Include(entry => entry.Lines)
            .Where(entry => entry.Household == household && entry.Store == receipt.Store && entry.Date == receipt.Date)
            .ToListAsync(cancellationToken);
        var existing = sameDay.FirstOrDefault(entry => entry.Total == receipt.Total);

        if (existing is not null)
        {
            return new ReceiptSaveResult(ToReceipt(existing), false);
        }

        var entry = new ReceiptEntry
        {
            Household = household,
            Store = receipt.Store,
            Date = receipt.Date,
            Total = receipt.Total,
            Savings = receipt.Savings,
            Deposits = receipt.Deposits,
            AddedBy = receipt.AddedBy,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            Lines = receipt.Lines.Select(line => new ReceiptLineEntry
            {
                ReceiptName = line.ReceiptName,
                FriendlyName = line.FriendlyName,
                Quantity = line.Quantity,
                Unit = line.Unit,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal,
                Discount = line.Discount
            }).ToList()
        };

        context.Receipts.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return new ReceiptSaveResult(ToReceipt(entry), true);
    }

    public async Task<IReadOnlyList<GroceryPrice>> ListLatestPricesAsync(
        string household,
        CancellationToken cancellationToken = default)
    {
        var receipts = await context.Receipts
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.Household == household)
            .ToListAsync(cancellationToken);

        return receipts
            .SelectMany(receipt => receipt.Lines.Select(line => (receipt.Date, receipt.Id, Line: line)))
            .GroupBy(entry => entry.Line.ReceiptName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.Date).ThenByDescending(entry => entry.Id).First())
            .Select(entry => new GroceryPrice(
                entry.Line.ReceiptName, entry.Line.FriendlyName, entry.Line.UnitPrice, entry.Line.Unit, entry.Date))
            .OrderBy(price => price.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<Receipt>> ListAsync(
        string household,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var entries = await context.Receipts
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.Household == household && entry.Date >= fromDate && entry.Date <= toDate)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return entries.Select(ToReceipt).ToList();
    }

    private static Receipt ToReceipt(ReceiptEntry entry) => new(
        entry.Id,
        entry.Store,
        entry.Date,
        entry.Total,
        entry.Savings,
        entry.Deposits,
        entry.AddedBy,
        entry.Lines
            .OrderBy(line => line.Id)
            .Select(line => new ReceiptLine(
                line.ReceiptName, line.FriendlyName, line.Quantity, line.Unit, line.UnitPrice, line.LineTotal, line.Discount))
            .ToList());
}
