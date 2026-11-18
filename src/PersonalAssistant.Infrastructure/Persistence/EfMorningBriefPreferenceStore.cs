using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.MorningBriefs;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class EfMorningBriefPreferenceStore(AssistantDbContext context, TimeProvider timeProvider)
    : IMorningBriefPreferenceStore
{
    public async Task<MorningBriefPreference?> GetAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var entry = await context.MorningBriefPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(preference => preference.PhoneNumber == phoneNumber, cancellationToken);

        return entry is null ? null : new MorningBriefPreference(entry.Enabled, entry.Time);
    }

    public async Task SetAsync(
        string phoneNumber,
        MorningBriefPreference preference,
        CancellationToken cancellationToken = default)
    {
        var entry = await context.MorningBriefPreferences
            .SingleOrDefaultAsync(saved => saved.PhoneNumber == phoneNumber, cancellationToken);

        if (entry is null)
        {
            entry = new MorningBriefPreferenceEntry { PhoneNumber = phoneNumber };
            context.MorningBriefPreferences.Add(entry);
        }

        entry.Enabled = preference.Enabled;
        entry.Time = preference.Enabled ? preference.Time : null;
        entry.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task ClearAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        context.MorningBriefPreferences
            .Where(preference => preference.PhoneNumber == phoneNumber)
            .ExecuteDeleteAsync(cancellationToken);
}
