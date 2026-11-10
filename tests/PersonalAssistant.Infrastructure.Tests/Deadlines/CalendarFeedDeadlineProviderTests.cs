using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Core.Deadlines;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Infrastructure.Calendars;
using PersonalAssistant.Infrastructure.Deadlines;
using PersonalAssistant.Infrastructure.Tests.Calendars;
using PersonalAssistant.Infrastructure.Tests.Claude;

namespace PersonalAssistant.Infrastructure.Tests.Deadlines;

public class CalendarFeedDeadlineProviderTests
{
    private static readonly TimeZoneInfo Dublin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin");
    // From 14 November, the next 14 days go up to 27 November: the 8 and 30 November deadlines are left out.
    private static readonly DateOnly FromDate = new(2026, 11, 14);

    private static readonly UserProfile Eduardo = new()
    {
        PhoneNumber = "353800000001",
        Name = "Eduardo",
        DeadlinesCalendarUrl = "https://example.com/moodle.ics?token=secret"
    };

    private static readonly string MoodleFeed = CalendarFeeds.Calendar(
        Due("20261108T235900Z", "CA-3 Capstone Project - 60% is due", "StratThink_HDSDAD_Feb26_FT"),
        Due("20261127T235900Z", "Integrated CA2 (50%) is due", "HDSDAD Feb26 FT |HDSDAD-06 |Machine Learning for Business"),
        Due("20261121T160000Z", "Lecture Quiz (Week 8) closes", "HDSDAD Feb26 FT |HDSDAD-06 |Machine Learning for Business"),
        Due("20261130T100000Z", "Final report is due", "Data Visualisation Techniques"));

    [Fact]
    public async Task GetUpcomingAsync_ReturnsNotConfigured_WithoutReadingAnyFeed()
    {
        var (provider, handler) = CreateProvider(HttpStatusCode.OK, MoodleFeed);
        var karina = new UserProfile { PhoneNumber = "353800000002", Name = "Karina" };

        var deadlines = await provider.GetUpcomingAsync(karina, FromDate);

        Assert.Equal(UpcomingDeadlinesStatus.NotConfigured, deadlines.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetUpcomingAsync_ReturnsTheNextFourteenDays_InDateOrder()
    {
        var (provider, _) = CreateProvider(HttpStatusCode.OK, MoodleFeed);

        var deadlines = await provider.GetUpcomingAsync(Eduardo, FromDate);

        Assert.Equal(UpcomingDeadlinesStatus.Available, deadlines.Status);
        Assert.Equal(
            new[] { "Lecture Quiz (Week 8) closes", "Integrated CA2 (50%) is due" },
            deadlines.Deadlines.Select(deadline => deadline.Title));
    }

    [Fact]
    public async Task GetUpcomingAsync_UsesTheLastCategory_AsTheCourse()
    {
        var (provider, _) = CreateProvider(HttpStatusCode.OK, MoodleFeed);

        var deadlines = await provider.GetUpcomingAsync(Eduardo, FromDate);

        Assert.All(deadlines.Deadlines, deadline => Assert.Equal("Machine Learning for Business", deadline.Course));
    }

    [Fact]
    public async Task GetUpcomingAsync_ConvertsUtcTimes_ToDublinSummerTime()
    {
        // 23:59 UTC on 6 October is 00:59 on 7 October in Dublin.
        var feed = CalendarFeeds.Calendar(Due("20261006T235900Z", "Essay is due", "Course"));
        var (provider, _) = CreateProvider(HttpStatusCode.OK, feed);

        var deadlines = await provider.GetUpcomingAsync(Eduardo, new DateOnly(2026, 10, 7));

        var deadline = Assert.Single(deadlines.Deadlines);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 0, 59, 0, TimeSpan.FromHours(1)), deadline.DueAt);
        Assert.Equal(TimeSpan.FromHours(1), deadline.DueAt.Offset);
    }

    [Fact]
    public async Task GetUpcomingAsync_ReturnsUnavailable_WhenTheFeedFails()
    {
        var (provider, _) = CreateProvider(HttpStatusCode.ServiceUnavailable, "Service Unavailable");

        var deadlines = await provider.GetUpcomingAsync(Eduardo, FromDate);

        Assert.Equal(UpcomingDeadlinesStatus.Unavailable, deadlines.Status);
    }

    private static string Due(string utc, string summary, string categories) =>
        CalendarFeeds.Event($"DTSTART:{utc}", $"DTEND:{utc}", $"SUMMARY:{summary}", $"CATEGORIES:{categories}");

    private static (CalendarFeedDeadlineProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var feed = new CachedCalendarFeed(
            new CalendarFeedClient(new HttpClient(handler)),
            new CalendarFeedCache(),
            TimeProvider.System,
            NullLogger<CachedCalendarFeed>.Instance);

        return (new CalendarFeedDeadlineProvider(feed, Dublin), handler);
    }
}
