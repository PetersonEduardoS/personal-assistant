using System.Collections.Concurrent;

namespace PersonalAssistant.Infrastructure.Calendars;

/// <summary>
/// Keeps, for each feed link, the last events read successfully and when the feed may be read again.
/// Registered once for the whole app, so the state survives between brief checks.
/// </summary>
public sealed class CalendarFeedCache
{
    private readonly ConcurrentDictionary<string, CalendarFeedCacheEntry> _entries = new(StringComparer.Ordinal);

    public CalendarFeedCacheEntry? Get(string url) => _entries.TryGetValue(url, out var entry) ? entry : null;

    public void Set(string url, CalendarFeedCacheEntry entry) => _entries[url] = entry;
}

/// <param name="LastGoodEvents">The last events read successfully, or null when every read failed.</param>
/// <param name="LastGoodAt">When <paramref name="LastGoodEvents"/> were read.</param>
/// <param name="NextReadAt">The feed is not read again before this time.</param>
public sealed record CalendarFeedCacheEntry(
    IReadOnlyList<CalendarEvent>? LastGoodEvents,
    DateTimeOffset LastGoodAt,
    DateTimeOffset NextReadAt);
