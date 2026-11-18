namespace PersonalAssistant.Core.Shopping;

/// <summary>
/// One product line of a supermarket receipt. <see cref="UnitPrice"/> is the regular price per unit or per kg,
/// before any offer; <see cref="Discount"/> is the offer taken off this line (a positive amount).
/// </summary>
public sealed record ReceiptLine(
    string ReceiptName,
    string FriendlyName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal LineTotal,
    decimal Discount)
{
    public const string Each = "un";

    public const string Kilogram = "kg";
}

/// <summary>A receipt read from a photo, before it is saved.</summary>
public sealed record NewReceipt(
    string Store,
    DateOnly Date,
    decimal Total,
    decimal Savings,
    decimal Deposits,
    string AddedBy,
    IReadOnlyList<ReceiptLine> Lines);

/// <summary>A saved receipt. Deposits are bottle deposits, part of the total but not a product.</summary>
public sealed record Receipt(
    long Id,
    string Store,
    DateOnly Date,
    decimal Total,
    decimal Savings,
    decimal Deposits,
    string AddedBy,
    IReadOnlyList<ReceiptLine> Lines);

public sealed record ReceiptSaveResult(Receipt Receipt, bool WasAdded);

/// <summary>The latest regular price paid for a product.</summary>
public sealed record GroceryPrice(string ReceiptName, string FriendlyName, decimal UnitPrice, string Unit, DateOnly Date);
