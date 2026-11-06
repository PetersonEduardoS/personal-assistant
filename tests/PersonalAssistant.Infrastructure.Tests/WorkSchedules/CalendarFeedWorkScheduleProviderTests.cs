using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;
using PersonalAssistant.Infrastructure.Tests.Claude;
using PersonalAssistant.Infrastructure.WorkSchedules;

namespace PersonalAssistant.Infrastructure.Tests.WorkSchedules;

public class CalendarFeedWorkScheduleProviderTests
{
    private const string FeedUrl = "https://example.com/ical.ics?key=secret";

    private static readonly TimeZoneInfo Dublin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin");

    private static readonly UserProfile Eduardo = new()
    {
        PhoneNumber = "353800000001",
        Name = "Eduardo",
        ShiftsCalendarUrl = FeedUrl
    };

    private static readonly string NovemberFeed = CalendarFeeds.Calendar(
        CalendarFeeds.Shift("20261105T170000Z", "20261105T230000Z"),
        CalendarFeeds.Shift("20261106T170000Z", "20261107T010000Z"),
        CalendarFeeds.DayOff("20261107", "20261108"),
        CalendarFeeds.Shift("20261108T133000Z", "20261108T210000Z"));

    [Fact]
    public async Task GetScheduleAsync_ReturnsNotConfigured_WithoutCallingTheFeed_WhenUserHasNoCalendarUrl()
    {
        var (provider, handler) = CreateProvider(HttpStatusCode.OK, NovemberFeed);
        var karina = new UserProfile { PhoneNumber = "353800000002", Name = "Karina" };

        var schedule = await provider.GetScheduleAsync(karina, new DateOnly(2026, 11, 6));

        Assert.Equal(WorkScheduleStatus.NotConfigured, schedule.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsOnlyTheShiftsThatStartOnThatDay()
    {
        var (provider, _) = CreateProvider(HttpStatusCode.OK, NovemberFeed);

        var schedule = await provider.GetScheduleAsync(Eduardo, new DateOnly(2026, 11, 6));

        Assert.Equal(WorkScheduleStatus.Available, schedule.Status);
        var shift = Assert.Single(schedule.Shifts);
        Assert.Equal(new WorkShift(new TimeOnly(17, 0), new TimeOnly(1, 0), "BAR"), shift);
        Assert.True(shift.EndsNextDay);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsNoShifts_OnADayOff()
    {
        var (provider, _) = CreateProvider(HttpStatusCode.OK, NovemberFeed);

        var schedule = await provider.GetScheduleAsync(Eduardo, new DateOnly(2026, 11, 7));

        Assert.Equal(WorkScheduleStatus.Available, schedule.Status);
        Assert.Empty(schedule.Shifts);
    }

    [Fact]
    public async Task GetScheduleAsync_ConvertsUtcTimes_ToDublinSummerTime()
    {
        var feed = CalendarFeeds.Calendar(CalendarFeeds.Shift("20261006T103000Z", "20261006T150000Z"));
        var (provider, _) = CreateProvider(HttpStatusCode.OK, feed);

        var schedule = await provider.GetScheduleAsync(Eduardo, new DateOnly(2026, 10, 6));

        var shift = Assert.Single(schedule.Shifts);
        Assert.Equal(new TimeOnly(11, 30), shift.Start);
        Assert.Equal(new TimeOnly(16, 0), shift.End);
    }

    [Fact]
    public async Task GetScheduleAsync_UsesTheDublinDay_ForAShiftThatStartsAfterMidnightInDublin()
    {
        // 23:30 UTC on 6 October is 00:30 on 7 October in Dublin.
        var feed = CalendarFeeds.Calendar(CalendarFeeds.Shift("20261006T233000Z", "20261007T040000Z"));
        var (provider, _) = CreateProvider(HttpStatusCode.OK, feed);

        var sixth = await provider.GetScheduleAsync(Eduardo, new DateOnly(2026, 10, 6));
        var seventh = await provider.GetScheduleAsync(Eduardo, new DateOnly(2026, 10, 7));

        Assert.Empty(sixth.Shifts);
        Assert.Equal(new TimeOnly(0, 30), Assert.Single(seventh.Shifts).Start);
    }

    [Fact]
    public async Task GetScheduleAsync_SortsShiftsByStartTime()
    {
        var feed = CalendarFeeds.Calendar(
            CalendarFeeds.Shift("20261110T180000Z", "20261110T230000Z", "BAR"),
            CalendarFeeds.Shift("20261110T090000Z", "20261110T130000Z", "FLOOR"));
        var (provider, _) = CreateProvider(HttpStatusCode.OK, feed);

        var schedule = await provider.GetScheduleAsync(Eduardo, new DateOnly(2026, 11, 10));

        Assert.Equal(new[] { "FLOOR", "BAR" }, schedule.Shifts.Select(shift => shift.Title));
    }

    [Fact]
    public async Task GetScheduleAsync_RequestsHttps_WhenUrlUsesWebcal()
    {
        var (provider, handler) = CreateProvider(HttpStatusCode.OK, NovemberFeed);
        var user = new UserProfile
        {
            PhoneNumber = "353800000001",
            Name = "Eduardo",
            ShiftsCalendarUrl = "webcal://example.com/ical.ics?key=secret"
        };

        await provider.GetScheduleAsync(user, new DateOnly(2026, 11, 6));

        Assert.Equal(new Uri(FeedUrl), handler.LastRequestUri);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsUnavailable_WhenTheFeedFails()
    {
        var (provider, _) = CreateProvider(HttpStatusCode.InternalServerError, "Server error");

        var schedule = await provider.GetScheduleAsync(Eduardo, new DateOnly(2026, 11, 6));

        Assert.Equal(WorkScheduleStatus.Unavailable, schedule.Status);
    }

    private static (CalendarFeedWorkScheduleProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var provider = new CalendarFeedWorkScheduleProvider(
            new HttpClient(handler),
            Dublin,
            NullLogger<CalendarFeedWorkScheduleProvider>.Instance);

        return (provider, handler);
    }
}
