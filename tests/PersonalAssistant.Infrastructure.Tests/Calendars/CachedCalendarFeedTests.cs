using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Infrastructure.Calendars;

namespace PersonalAssistant.Infrastructure.Tests.Calendars;

public class CachedCalendarFeedTests
{
    private const string FeedUrl = "https://example.com/one";
    private const string Description = "test feed";

    private static readonly DateTimeOffset Now = new(2026, 11, 6, 6, 0, 0, TimeSpan.Zero);

    private static readonly string Feed =
        CalendarFeeds.Calendar(CalendarFeeds.Shift("20261106T170000Z", "20261107T010000Z"));

    private readonly SwitchableHttpMessageHandler _handler = new(HttpStatusCode.OK, Feed);
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly CalendarFeedCache _cache = new();

    [Fact]
    public async Task GetEventsAsync_ReusesTheEvents_WithinTheCacheLifetime()
    {
        var first = await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _clock.Now = Now + CachedCalendarFeed.CacheLifetime - TimeSpan.FromMinutes(1);

        var second = await CreateFeed().GetEventsAsync(FeedUrl, Description);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, _handler.RequestCount);
    }

    [Fact]
    public async Task GetEventsAsync_ReadsTheFeedAgain_AfterTheCacheLifetime()
    {
        await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _clock.Now = Now + CachedCalendarFeed.CacheLifetime;

        await CreateFeed().GetEventsAsync(FeedUrl, Description);

        Assert.Equal(2, _handler.RequestCount);
    }

    [Fact]
    public async Task GetEventsAsync_WaitsBeforeTryingAgain_AfterAFailure()
    {
        _handler.Fail();
        var failed = await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _handler.Succeed(Feed);

        _clock.Now = Now + CachedCalendarFeed.RetryAfterFailure - TimeSpan.FromMinutes(1);
        var whileWaiting = await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _clock.Now = Now + CachedCalendarFeed.RetryAfterFailure;
        var afterWaiting = await CreateFeed().GetEventsAsync(FeedUrl, Description);

        Assert.Null(failed);
        Assert.Null(whileWaiting);
        Assert.NotNull(afterWaiting);
        Assert.Equal(2, _handler.RequestCount);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsTheLastGoodEvents_WhenTheFeedFails()
    {
        var good = await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _handler.Fail();
        _clock.Now = Now + CachedCalendarFeed.CacheLifetime;

        var events = await CreateFeed().GetEventsAsync(FeedUrl, Description);

        Assert.Same(good, events);
        Assert.Equal(2, _handler.RequestCount);
    }

    [Fact]
    public async Task GetEventsAsync_KeepsUsingTheLastGoodEvents_WhileWaitingToRetry()
    {
        var good = await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _handler.Fail();
        _clock.Now = Now + CachedCalendarFeed.CacheLifetime;
        await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _clock.Now += TimeSpan.FromMinutes(10);

        var events = await CreateFeed().GetEventsAsync(FeedUrl, Description);

        Assert.Same(good, events);
        Assert.Equal(2, _handler.RequestCount);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsNull_WhenTheLastGoodEventsAreTooOld()
    {
        await CreateFeed().GetEventsAsync(FeedUrl, Description);
        _handler.Fail();
        _clock.Now = Now + CachedCalendarFeed.LastGoodLifetime;

        var events = await CreateFeed().GetEventsAsync(FeedUrl, Description);

        Assert.Null(events);
    }

    [Fact]
    public async Task GetEventsAsync_KeepsLinksApart()
    {
        await CreateFeed().GetEventsAsync(FeedUrl, Description);
        await CreateFeed().GetEventsAsync("https://example.com/two", Description);

        Assert.Equal(2, _handler.RequestCount);
    }

    [Fact]
    public async Task GetEventsAsync_RequestsHttps_WhenUrlUsesWebcal()
    {
        await CreateFeed().GetEventsAsync("webcal://example.com/ical.ics?key=secret", Description);

        Assert.Equal(new Uri("https://example.com/ical.ics?key=secret"), Assert.Single(_handler.RequestUris));
    }

    private CachedCalendarFeed CreateFeed() =>
        new(new CalendarFeedClient(new HttpClient(_handler)), _cache, _clock, NullLogger<CachedCalendarFeed>.Instance);
}
