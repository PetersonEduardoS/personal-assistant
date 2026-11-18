namespace PersonalAssistant.Core.Shopping;

/// <summary>
/// Keeps the shopping list of each household. Only people of the same household see and change it.
/// </summary>
public interface IShoppingListStore
{
    /// <summary>
    /// Adds an item, or returns the pending item with the same name (ignoring case) without adding it again.
    /// </summary>
    Task<ShoppingAddResult> AddAsync(
        string household,
        string name,
        string? quantity,
        string section,
        string addedBy,
        CancellationToken cancellationToken = default);

    /// <summary>The items not bought yet, oldest first.</summary>
    Task<IReadOnlyList<ShoppingItem>> ListPendingAsync(string household, CancellationToken cancellationToken = default);

    /// <summary>Marks these pending items as bought and returns how many were marked.</summary>
    Task<int> MarkBoughtAsync(string household, IReadOnlyCollection<long> itemIds, CancellationToken cancellationToken = default);

    /// <summary>Removes a pending item and returns it, or null when it is not on this household's list.</summary>
    Task<ShoppingItem?> RemoveAsync(string household, long itemId, CancellationToken cancellationToken = default);
}
