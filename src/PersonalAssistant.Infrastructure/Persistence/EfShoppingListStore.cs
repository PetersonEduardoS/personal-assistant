using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.Shopping;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class EfShoppingListStore(AssistantDbContext context, TimeProvider timeProvider) : IShoppingListStore
{
    public async Task<ShoppingAddResult> AddAsync(
        string household,
        string name,
        string? quantity,
        string section,
        string addedBy,
        CancellationToken cancellationToken = default)
    {
        var cleanName = name.Trim();

        if (cleanName.Length == 0 || cleanName.Length > ShoppingItem.MaxNameLength)
        {
            throw new ArgumentException("The item needs a name of up to 100 characters.", nameof(name));
        }

        // The pending list is short, so the duplicate check runs in memory, where case rules cover accents too.
        var existing = (await PendingQuery(household).ToListAsync(cancellationToken))
            .FirstOrDefault(entry => string.Equals(entry.Name, cleanName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return new ShoppingAddResult(ToItem(existing), false);
        }

        var entry = new ShoppingItemEntry
        {
            Household = household,
            Name = cleanName,
            Quantity = string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim(),
            Section = ShoppingSections.Normalize(section),
            AddedBy = addedBy,
            AddedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        context.ShoppingItems.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return new ShoppingAddResult(ToItem(entry), true);
    }

    public async Task<IReadOnlyList<ShoppingItem>> ListPendingAsync(
        string household,
        CancellationToken cancellationToken = default) =>
        (await PendingQuery(household).AsNoTracking().ToListAsync(cancellationToken)).Select(ToItem).ToList();

    public Task<int> MarkBoughtAsync(
        string household,
        IReadOnlyCollection<long> itemIds,
        CancellationToken cancellationToken = default)
    {
        DateTime? boughtAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        // A single UPDATE without ordering, so a second "comprei" for the same items changes nothing.
        return context.ShoppingItems
            .Where(entry => entry.Household == household && entry.BoughtAtUtc == null && itemIds.Contains(entry.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.BoughtAtUtc, boughtAtUtc), cancellationToken);
    }

    public async Task<ShoppingItem?> RemoveAsync(string household, long itemId, CancellationToken cancellationToken = default)
    {
        var entry = await PendingQuery(household).SingleOrDefaultAsync(item => item.Id == itemId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        context.ShoppingItems.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);

        return ToItem(entry);
    }

    private IQueryable<ShoppingItemEntry> PendingQuery(string household) =>
        context.ShoppingItems
            .Where(entry => entry.Household == household && entry.BoughtAtUtc == null)
            .OrderBy(entry => entry.Id);

    private static ShoppingItem ToItem(ShoppingItemEntry entry) =>
        new(entry.Id, entry.Name, entry.Quantity, entry.Section, entry.AddedBy, UtcDateTimes.ToOffset(entry.AddedAtUtc));
}
