namespace PersonalAssistant.Core.Shopping;

/// <summary>An item on the household shopping list. <see cref="AddedBy"/> is the name of who asked for it.</summary>
public sealed record ShoppingItem(
    long Id,
    string Name,
    string? Quantity,
    string Section,
    string AddedBy,
    DateTimeOffset AddedAt)
{
    public const int MaxNameLength = 100;

    public const int MaxQuantityLength = 50;
}

/// <summary>The item on the list, and whether it was added now or was already there.</summary>
public sealed record ShoppingAddResult(ShoppingItem Item, bool WasAdded);
