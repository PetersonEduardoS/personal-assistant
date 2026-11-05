using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.MorningBriefs;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class EfMorningBriefStore(AssistantDbContext context, TimeProvider timeProvider) : IMorningBriefStore
{
    public async Task<MorningBrief?> GetAsync(
        string phoneNumber,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var entry = await context.MorningBriefs
            .AsNoTracking()
            .SingleOrDefaultAsync(brief => brief.PhoneNumber == phoneNumber && brief.Date == date, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        DateTimeOffset? deliveredAt = entry.DeliveredAtUtc is null ? null : UtcDateTimes.ToOffset(entry.DeliveredAtUtc.Value);

        return new MorningBrief(entry.PhoneNumber, entry.Date, entry.Content, deliveredAt);
    }

    public async Task<bool> TryAddAsync(
        string phoneNumber,
        DateOnly date,
        string content,
        CancellationToken cancellationToken = default)
    {
        var alreadyStored = await context.MorningBriefs
            .AnyAsync(brief => brief.PhoneNumber == phoneNumber && brief.Date == date, cancellationToken);

        if (alreadyStored)
        {
            return false;
        }

        context.MorningBriefs.Add(new MorningBriefEntry
        {
            PhoneNumber = phoneNumber,
            Date = date,
            Content = content,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception) when (SqliteErrors.IsConstraintViolation(exception))
        {
            // Another brief for the same day was saved at the same moment.
            context.ChangeTracker.Clear();

            return false;
        }
    }

    public async Task<bool> TryMarkDeliveredAsync(
        string phoneNumber,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        DateTime? deliveredAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        // A single UPDATE that only changes an undelivered brief, so only one caller can win.
        var updatedRows = await context.MorningBriefs
            .Where(brief => brief.PhoneNumber == phoneNumber && brief.Date == date && brief.DeliveredAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(brief => brief.DeliveredAtUtc, deliveredAtUtc),
                cancellationToken);

        return updatedRows == 1;
    }
}
