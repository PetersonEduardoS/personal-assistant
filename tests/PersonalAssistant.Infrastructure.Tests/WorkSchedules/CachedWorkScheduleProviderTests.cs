using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;
using PersonalAssistant.Infrastructure.WorkSchedules;

namespace PersonalAssistant.Infrastructure.Tests.WorkSchedules;

public class CachedWorkScheduleProviderTests
{
    private static readonly DateOnly Friday = new(2026, 11, 6);
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 6, 0, 0, TimeSpan.Zero);

    private static readonly string FridayFeed =
        CalendarFeeds.Calendar(CalendarFeeds.Shift("20261106T170000Z", "20261107T010000Z"));

    private readonly SwitchableHttpMessageHandler _feed = new(HttpStatusCode.OK, FridayFeed);
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly WorkScheduleCache _cache = new();

    private readonly UserProfile _eduardo = new()
    {
        PhoneNumber = "353800000001",
        Name = "Eduardo",
        ShiftsCalendarUrl = "https://example.com/one"
    };

    [Fact]
    public async Task GetScheduleAsync_ReusesTheSchedule_WithinTheCacheLifetime()
    {
        var first = await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _clock.Now = Now + CachedWorkScheduleProvider.CacheLifetime - TimeSpan.FromMinutes(1);

        var second = await CreateProvider().GetScheduleAsync(_eduardo, Friday);

        Assert.Same(first, second);
        Assert.Equal(1, _feed.RequestCount);
    }

    [Fact]
    public async Task GetScheduleAsync_ReadsTheFeedAgain_AfterTheCacheLifetime()
    {
        await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _clock.Now = Now + CachedWorkScheduleProvider.CacheLifetime;

        await CreateProvider().GetScheduleAsync(_eduardo, Friday);

        Assert.Equal(2, _feed.RequestCount);
    }

    [Fact]
    public async Task GetScheduleAsync_WaitsBeforeTryingAgain_AfterAFailure()
    {
        _feed.Fail();
        var failed = await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _feed.Succeed(FridayFeed);

        _clock.Now = Now + CachedWorkScheduleProvider.RetryAfterFailure - TimeSpan.FromMinutes(1);
        var whileWaiting = await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _clock.Now = Now + CachedWorkScheduleProvider.RetryAfterFailure;
        var afterWaiting = await CreateProvider().GetScheduleAsync(_eduardo, Friday);

        Assert.Equal(WorkScheduleStatus.Unavailable, failed.Status);
        Assert.Equal(WorkScheduleStatus.Unavailable, whileWaiting.Status);
        Assert.Equal(WorkScheduleStatus.Available, afterWaiting.Status);
        Assert.Equal(2, _feed.RequestCount);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsTheLastGoodSchedule_WhenTheFeedFails()
    {
        var good = await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _feed.Fail();
        _clock.Now = Now + CachedWorkScheduleProvider.CacheLifetime;

        var schedule = await CreateProvider().GetScheduleAsync(_eduardo, Friday);

        Assert.Same(good, schedule);
        Assert.Equal(2, _feed.RequestCount);
    }

    [Fact]
    public async Task GetScheduleAsync_KeepsUsingTheLastGoodSchedule_WhileWaitingToRetry()
    {
        var good = await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _feed.Fail();
        _clock.Now = Now + CachedWorkScheduleProvider.CacheLifetime;
        await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _clock.Now += TimeSpan.FromMinutes(10);

        var schedule = await CreateProvider().GetScheduleAsync(_eduardo, Friday);

        Assert.Same(good, schedule);
        Assert.Equal(2, _feed.RequestCount);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsUnavailable_WhenTheLastGoodScheduleIsTooOld()
    {
        await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        _feed.Fail();
        _clock.Now = Now + CachedWorkScheduleProvider.LastGoodLifetime;

        var schedule = await CreateProvider().GetScheduleAsync(_eduardo, Friday);

        Assert.Equal(WorkScheduleStatus.Unavailable, schedule.Status);
    }

    [Fact]
    public async Task GetScheduleAsync_KeepsDaysAndLinksApart()
    {
        await CreateProvider().GetScheduleAsync(_eduardo, Friday);
        await CreateProvider().GetScheduleAsync(_eduardo, Friday.AddDays(1));
        _eduardo.ShiftsCalendarUrl = "https://example.com/two";
        await CreateProvider().GetScheduleAsync(_eduardo, Friday);

        Assert.Equal(3, _feed.RequestCount);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsNotConfigured_WithoutReadingAnyFeed()
    {
        var karina = new UserProfile { PhoneNumber = "353800000002", Name = "Karina" };

        var schedule = await CreateProvider().GetScheduleAsync(karina, Friday);

        Assert.Equal(WorkScheduleStatus.NotConfigured, schedule.Status);
        Assert.Equal(0, _feed.RequestCount);
    }

    private CachedWorkScheduleProvider CreateProvider()
    {
        var inner = new CalendarFeedWorkScheduleProvider(
            new HttpClient(_feed),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"),
            NullLogger<CalendarFeedWorkScheduleProvider>.Instance);

        return new CachedWorkScheduleProvider(inner, _cache, _clock, NullLogger<CachedWorkScheduleProvider>.Instance);
    }

    /// <summary>
    /// A fake feed that can start failing or succeeding in the middle of a test.
    /// </summary>
    private sealed class SwitchableHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = statusCode;
        private string _body = body;

        public int RequestCount { get; private set; }

        public void Fail()
        {
            _statusCode = HttpStatusCode.ServiceUnavailable;
            _body = "Service Unavailable";
        }

        public void Succeed(string body)
        {
            _statusCode = HttpStatusCode.OK;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "text/calendar")
            });
        }
    }
}
