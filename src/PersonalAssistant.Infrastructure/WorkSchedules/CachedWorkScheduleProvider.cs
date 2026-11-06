using Microsoft.Extensions.Caching.Memory;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Infrastructure.WorkSchedules;

/// <summary>Avoids fetching the same private feed every minute while checking dynamic brief times.</summary>
public sealed class CachedWorkScheduleProvider(
    CalendarFeedWorkScheduleProvider inner, IMemoryCache cache) : IWorkScheduleProvider
{
    public async Task<WorkSchedule> GetScheduleAsync(UserProfile user, DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var key = (typeof(CachedWorkScheduleProvider), user.PhoneNumber, user.ShiftsCalendarUrl, date);
        if (cache.TryGetValue(key, out WorkSchedule? cached)) return cached!;
        var schedule = await inner.GetScheduleAsync(user, date, cancellationToken);
        cache.Set(key, schedule, schedule.Status == WorkScheduleStatus.Unavailable
            ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5));
        return schedule;
    }
}
