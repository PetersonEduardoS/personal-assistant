using Microsoft.Extensions.Logging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Infrastructure.WorkSchedules;

/// <summary>
/// Reads the private shifts feed only now and then, while the brief time is checked every minute.
/// When a read fails, the last good schedule of that day is used, so a short outage does not hide the shifts.
/// </summary>
public sealed class CachedWorkScheduleProvider(
    CalendarFeedWorkScheduleProvider inner,
    WorkScheduleCache cache,
    TimeProvider clock,
    ILogger<CachedWorkScheduleProvider> logger) : IWorkScheduleProvider
{
    /// <summary>How long a schedule read successfully is reused. A change in the app shows up within this time.</summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(120);

    /// <summary>How long to wait after a failed read, so a busy feed is not asked again right away.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(30);

    /// <summary>How long the last good schedule can stand in for a failed read.</summary>
    public static readonly TimeSpan LastGoodLifetime = TimeSpan.FromHours(24);

    public async Task<WorkSchedule> GetScheduleAsync(
        UserProfile user,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.ShiftsCalendarUrl))
        {
            return WorkSchedule.NotConfigured;
        }

        var key = new WorkScheduleCacheKey(user.PhoneNumber, user.ShiftsCalendarUrl, date);
        var entry = cache.Get(key);
        var now = clock.GetUtcNow();

        if (entry is not null && now < entry.NextReadAt)
        {
            return LastGoodOrUnavailable(entry, now);
        }

        var schedule = await inner.GetScheduleAsync(user, date, cancellationToken);

        if (schedule.Status == WorkScheduleStatus.Available)
        {
            cache.Set(key, new WorkScheduleCacheEntry(schedule, now, now + CacheLifetime));

            return schedule;
        }

        var failedEntry = new WorkScheduleCacheEntry(
            entry?.LastGoodSchedule,
            entry?.LastGoodAt ?? now,
            now + RetryAfterFailure);
        cache.Set(key, failedEntry);

        var fallback = LastGoodOrUnavailable(failedEntry, now);

        if (fallback.Status == WorkScheduleStatus.Available)
        {
            logger.LogInformation(
                "Using the last good work schedule of {UserName}, read at {LastGoodAt:u}",
                user.Name,
                failedEntry.LastGoodAt);
        }

        return fallback;
    }

    private static WorkSchedule LastGoodOrUnavailable(WorkScheduleCacheEntry entry, DateTimeOffset now) =>
        entry.LastGoodSchedule is not null && now - entry.LastGoodAt < LastGoodLifetime
            ? entry.LastGoodSchedule
            : WorkSchedule.Unavailable;
}
