namespace PersonalAssistant.Core.Shopping;

/// <summary>Keeps the supermarket receipts of each household and the prices they show.</summary>
public interface IReceiptStore
{
    /// <summary>
    /// Saves the receipt, or returns the one already saved with the same store, date and total
    /// (the same receipt sent twice).
    /// </summary>
    Task<ReceiptSaveResult> AddAsync(string household, NewReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>The most recent price of each product the household has bought.</summary>
    Task<IReadOnlyList<GroceryPrice>> ListLatestPricesAsync(string household, CancellationToken cancellationToken = default);

    /// <summary>The receipts from <paramref name="fromDate"/> to <paramref name="toDate"/>, both included.</summary>
    Task<IReadOnlyList<Receipt>> ListAsync(
        string household,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}
