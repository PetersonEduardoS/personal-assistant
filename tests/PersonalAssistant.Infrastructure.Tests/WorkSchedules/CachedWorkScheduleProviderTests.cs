using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Infrastructure.Tests.Claude;
using PersonalAssistant.Infrastructure.WorkSchedules;

namespace PersonalAssistant.Infrastructure.Tests.WorkSchedules;

public class CachedWorkScheduleProviderTests
{
    [Fact]
    public async Task GetScheduleAsync_ReusesFeedAcrossProviderInstancesButSeparatesDatesAndUsers()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            CalendarFeeds.Calendar(CalendarFeeds.Shift("20261106T080000Z", "20261106T160000Z")));
        var inner = new CalendarFeedWorkScheduleProvider(new HttpClient(handler),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"), NullLogger<CalendarFeedWorkScheduleProvider>.Instance);
        var user = new UserProfile { PhoneNumber = "1", ShiftsCalendarUrl = "https://example.com/one" };
        var first = await new CachedWorkScheduleProvider(inner, cache).GetScheduleAsync(user, new(2026, 11, 6));
        var second = await new CachedWorkScheduleProvider(inner, cache).GetScheduleAsync(user, new(2026, 11, 6));
        Assert.Same(first, second);
        Assert.Single(handler.Requests);
        await new CachedWorkScheduleProvider(inner, cache).GetScheduleAsync(user, new(2026, 11, 7));
        user.ShiftsCalendarUrl = "https://example.com/two";
        await new CachedWorkScheduleProvider(inner, cache).GetScheduleAsync(user, new(2026, 11, 6));
        Assert.Equal(3, handler.Requests.Count);
    }
}
