namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>A supermarket receipt of a household.</summary>
public sealed class ReceiptEntry
{
    public long Id { get; set; }

    public string Household { get; set; } = string.Empty;

    public string Store { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public decimal Total { get; set; }

    public decimal Savings { get; set; }

    public decimal Deposits { get; set; }

    public string AddedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public List<ReceiptLineEntry> Lines { get; set; } = [];
}

/// <summary>A product line of a receipt, in the order printed.</summary>
public sealed class ReceiptLineEntry
{
    public long Id { get; set; }

    public long ReceiptId { get; set; }

    public string ReceiptName { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public decimal Discount { get; set; }
}
