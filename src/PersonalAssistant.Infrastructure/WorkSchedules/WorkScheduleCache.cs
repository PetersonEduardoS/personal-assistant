using System.Collections.Concurrent;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Infrastructure.WorkSchedules;

/// <summary>
/// Keeps, for each user, link and day, the last good schedule and when the feed may be read again.
/// Registered once for the whole app, so the state survives between brief checks.
/// </summary>
public sealed class WorkScheduleCache
{
    private const int DaysToKeep = 2;

    private readonly ConcurrentDictionary<WorkScheduleCacheKey, WorkScheduleCacheEntry> _entries = new();

    public WorkScheduleCacheEntry? Get(WorkScheduleCacheKey key) =>
        _entries.TryGetValue(key, out var entry) ? entry : null;

    public void Set(WorkScheduleCacheKey key, WorkScheduleCacheEntry entry)
    {
        _entries[key] = entry;

        // Old days are never asked again, so they are dropped to keep the cache small.
        foreach (var oldKey in _entries.Keys.Where(existing => existing.Date < key.Date.AddDays(-DaysToKeep)))
        {
            _entries.TryRemove(oldKey, out _);
        }
    }
}

public sealed record WorkScheduleCacheKey(string PhoneNumber, string CalendarUrl, DateOnly Date);

/// <param name="LastGoodSchedule">The last schedule read successfully, or null when every read failed.</param>
/// <param name="LastGoodAt">When <paramref name="LastGoodSchedule"/> was read.</param>
/// <param name="NextReadAt">The feed is not read again before this time.</param>
public sealed record WorkScheduleCacheEntry(
    WorkSchedule? LastGoodSchedule,
    DateTimeOffset LastGoodAt,
    DateTimeOffset NextReadAt);
