using Microsoft.Extensions.Logging;

namespace PersonalAssistant.Infrastructure.Calendars;

/// <summary>
/// Reads private calendar feeds only now and then, while the brief time is checked every minute.
/// When a read fails, the last good copy is used, so a short outage does not hide the events.
/// One download serves every day and every use of the same feed.
/// </summary>
public sealed class CachedCalendarFeed(
    CalendarFeedClient client,
    CalendarFeedCache cache,
    TimeProvider clock,
    ILogger<CachedCalendarFeed> logger)
{
    /// <summary>How long a successful read is reused. A change in the app shows up within this time.</summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(120);

    /// <summary>How long to wait after a failed read, so a busy feed is not asked again right away.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(30);

    /// <summary>How long the last good copy can stand in for a failed read.</summary>
    public static readonly TimeSpan LastGoodLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Returns the feed's events, or null when it cannot be read and there is no recent good copy.
    /// The link is a secret, so logs only use <paramref name="feedDescription"/>.
    /// </summary>
    public async Task<IReadOnlyList<CalendarEvent>?> GetEventsAsync(
        string url,
        string feedDescription,
        CancellationToken cancellationToken = default)
    {
        var entry = cache.Get(url);
        var now = clock.GetUtcNow();

        if (entry is not null && now < entry.NextReadAt)
        {
            return LastGoodOrNull(entry, now);
        }

        try
        {
            var events = await client.GetEventsAsync(url, cancellationToken);
            cache.Set(url, new CalendarFeedCacheEntry(events, now, now + CacheLifetime));

            return events;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Could not read the {FeedDescription}: {FailureType}, HTTP {StatusCode}",
                feedDescription, exception.GetType().Name, (exception as HttpRequestException)?.StatusCode);

            var failedEntry = new CalendarFeedCacheEntry(
                entry?.LastGoodEvents,
                entry?.LastGoodAt ?? now,
                now + RetryAfterFailure);
            cache.Set(url, failedEntry);

            var fallback = LastGoodOrNull(failedEntry, now);

            if (fallback is not null)
            {
                logger.LogInformation("Using the last good copy of the {FeedDescription}, read at {LastGoodAt:u}",
                    feedDescription, failedEntry.LastGoodAt);
            }

            return fallback;
        }
    }

    private static IReadOnlyList<CalendarEvent>? LastGoodOrNull(CalendarFeedCacheEntry entry, DateTimeOffset now) =>
        entry.LastGoodEvents is not null && now - entry.LastGoodAt < LastGoodLifetime
            ? entry.LastGoodEvents
            : null;
}
