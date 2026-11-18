namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>An item on a household shopping list. Bought items keep their row, with the time they were bought.</summary>
public sealed class ShoppingItemEntry
{
    public long Id { get; set; }

    public string Household { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Quantity { get; set; }

    public string Section { get; set; } = string.Empty;

    public string AddedBy { get; set; } = string.Empty;

    public DateTime AddedAtUtc { get; set; }

    public DateTime? BoughtAtUtc { get; set; }
}
