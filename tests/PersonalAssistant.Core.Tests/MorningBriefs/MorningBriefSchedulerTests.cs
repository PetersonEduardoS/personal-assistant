using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Core.Tests.MorningBriefs;

public class MorningBriefSchedulerTests
{
    [Theory]
    [InlineData("2026-11-06T07:29:00Z", false)]
    [InlineData("2026-11-06T07:30:00Z", true)]
    [InlineData("2026-11-06T08:29:59Z", true)]
    [InlineData("2026-11-06T08:30:00Z", false)]
    [InlineData("2026-07-06T06:30:00Z", true)]
    [InlineData("2026-07-06T07:30:00Z", false)]
    public async Task RunAsync_UsesDublinTimeAndBoundedCatchUp(string timestamp, bool due)
    {
        var fixture = new Fixture(DateTimeOffset.Parse(timestamp));
        var result = await fixture.Scheduler.RunAsync(fixture.User);
        Assert.Equal(due ? MorningBriefResult.TemplateSent : MorningBriefResult.NotDue, result);
        Assert.Equal(due ? 1 : 0, fixture.Assistant.ReceivedRequests.Count);
    }

    [Fact]
    public async Task RunAsync_UnconfiguredUserDoesNothing()
    {
        var fixture = new Fixture();
        fixture.User.MorningBriefTime = null;
        Assert.Equal(MorningBriefResult.NotDue, await fixture.Scheduler.RunAsync(fixture.User));
        Assert.Empty(fixture.Assistant.ReceivedRequests);
    }

    [Theory]
    [InlineData(23, true)]
    [InlineData(24, false)]
    [InlineData(25, false)]
    public async Task RunAsync_ChoosesTextOrTemplateByWindow(int hoursAgo, bool open)
    {
        var fixture = new Fixture();
        await fixture.Conversations.TryAddIncomingMessageAsync(fixture.User.PhoneNumber, "incoming", "Oi",
            fixture.Now.AddHours(-hoursAgo));

        var result = await fixture.Scheduler.RunAsync(fixture.User);

        Assert.Equal(open ? MorningBriefResult.TextSent : MorningBriefResult.TemplateSent, result);
        Assert.Equal(open, fixture.Store.Brief!.IsDelivered);
        Assert.Equal(open ? 1 : 0, fixture.Sender.Texts);
        Assert.Equal(open ? 0 : 1, fixture.Sender.Templates);
        Assert.Equal(open ? 2 : 1, fixture.Conversations.Turns.Count);
        if (open) Assert.Equal("Bom dia!", fixture.Conversations.Turns.Last().Turn.Content);
    }

    [Fact]
    public async Task RunAsync_RestartDoesNotRegenerateOrResendTemplate()
    {
        var fixture = new Fixture();
        await fixture.Scheduler.RunAsync(fixture.User);
        Assert.Equal(MorningBriefResult.AlreadyPrepared, await fixture.CreateScheduler().RunAsync(fixture.User));
        Assert.Single(fixture.Assistant.ReceivedRequests);
        Assert.Equal(1, fixture.Sender.Templates);
        Assert.False(fixture.Store.Brief!.IsDelivered);
        Assert.Equal(new DateOnly(2026, 11, 6), fixture.Store.Brief.Date);
        Assert.Equal("Bom dia!", fixture.Store.Brief.Content);
    }

    [Fact]
    public async Task RunAsync_LosingInsertDoesNotSend()
    {
        var fixture = new Fixture();
        fixture.Store.RejectInsert = true;
        Assert.Equal(MorningBriefResult.AlreadyPrepared, await fixture.Scheduler.RunAsync(fixture.User));
        Assert.Equal(0, fixture.Sender.Templates);
    }

    [Fact]
    public async Task RunAsync_SendFailureKeepsBriefWithoutAutomaticResend()
    {
        var fixture = new Fixture();
        fixture.Sender.Fail = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Scheduler.RunAsync(fixture.User));
        Assert.False(fixture.Store.Brief!.IsDelivered);
        Assert.Equal(MorningBriefResult.AlreadyPrepared, await fixture.Scheduler.RunAsync(fixture.User));
        Assert.Equal(1, fixture.Sender.Templates);
    }

    [Fact]
    public async Task RunAsync_UnavailableScheduleStillGeneratesBrief()
    {
        var fixture = new Fixture();
        await fixture.Scheduler.RunAsync(fixture.User);
        Assert.Contains(MorningBriefWriter.UnknownShiftsLine,
            fixture.Assistant.ReceivedRequests.Single().Conversation.Single().Content);
    }

    [Fact]
    public async Task RunAsync_WriterFailureDoesNotClaimTheDay()
    {
        var fixture = new Fixture(assistant: FakeAssistantClient.Failing());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Scheduler.RunAsync(fixture.User));
        Assert.Null(fixture.Store.Brief);
        Assert.Equal(0, fixture.Sender.Templates);
    }

    [Fact]
    public async Task RunAsync_EmptyBriefIsNotStoredOrSent()
    {
        var fixture = new Fixture(assistant: new FakeAssistantClient(" "));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Scheduler.RunAsync(fixture.User));
        Assert.Null(fixture.Store.Brief);
        Assert.Equal(0, fixture.Sender.Templates);
    }

    [Theory]
    [InlineData("2026-11-05T07:34:00Z", false)]
    [InlineData("2026-11-05T07:35:00Z", true)]
    [InlineData("2026-11-05T08:35:00Z", false)]
    [InlineData("2026-07-02T06:35:00Z", true)]
    public async Task RunAsync_DynamicRoutineOverridesFixedTimeInDublin(string timestamp, bool due)
    {
        var fixture = new Fixture(DateTimeOffset.Parse(timestamp));
        fixture.User.MorningBriefTime = null;
        fixture.Routine = new()
        {
            Appointments = [new() { DayOfWeek = DayOfWeek.Thursday, From = new(2026, 1, 1), Until = new(2026, 12, 31), Start = new(8, 30), TravelMinutes = 25 }]
        };
        Assert.Equal(due ? MorningBriefResult.TemplateSent : MorningBriefResult.NotDue,
            await fixture.CreateScheduler().RunAsync(fixture.User));
    }

    [Fact]
    public async Task RunNowAsync_BypassesTimeButRemainsOncePerDay()
    {
        var fixture = new Fixture(DateTimeOffset.Parse("2026-11-06T03:55:00Z"));
        fixture.User.MorningBriefTime = null;
        Assert.Equal(MorningBriefResult.TemplateSent, await fixture.Scheduler.RunNowAsync(fixture.User));
        Assert.Equal(MorningBriefResult.AlreadyPrepared, await fixture.Scheduler.RunNowAsync(fixture.User));
        Assert.Equal(1, fixture.Sender.Templates);
    }

    [Fact]
    public async Task RunAsync_FillsApprovedTemplateFieldsWithUserAndLocalDate()
    {
        var fixture = new Fixture();
        await fixture.Scheduler.RunAsync(fixture.User);
        Assert.Equal(new[] { "Eduardo", "sexta, 6 de novembro" }, fixture.Sender.BodyParameters);
    }

    private sealed class Fixture
    {
        public DateTimeOffset Now { get; }
        public UserProfile User { get; } = new() { Name = "Eduardo", PhoneNumber = "353800000001", MorningBriefTime = new(7, 30) };
        public FakeAssistantClient Assistant { get; }
        public FakeConversationStore Conversations { get; } = new();
        public Store Store { get; } = new();
        public Sender Sender { get; } = new();
        public MorningBriefRoutine? Routine { get; set; }
        public MorningBriefScheduler Scheduler { get; }
        public Fixture(DateTimeOffset? now = null, FakeAssistantClient? assistant = null)
        {
            Now = now ?? DateTimeOffset.Parse("2026-11-06T07:30:00Z");
            Assistant = assistant ?? new FakeAssistantClient("Bom dia!");
            Scheduler = CreateScheduler();
        }
        public MorningBriefScheduler CreateScheduler() => new(
            new MorningBriefWriter(Assistant, new FakeUserContextProvider()), new Schedules(), Store,
            Conversations, Sender, new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"), new Routines(Routine),
            new MorningBriefDelivery(Store, Sender, Conversations, new MorningBriefDeliveryGate()));
    }

    private sealed class Schedules : IWorkScheduleProvider
    {
        public Task<WorkSchedule> GetScheduleAsync(UserProfile user, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(WorkSchedule.Unavailable);
    }

    private sealed class Routines(MorningBriefRoutine? routine) : IMorningBriefRoutineProvider
    {
        public Task<MorningBriefRoutine?> GetAsync(UserProfile user, CancellationToken cancellationToken = default) =>
            Task.FromResult(routine);
    }

    private sealed class Store : IMorningBriefStore
    {
        public MorningBrief? Brief { get; private set; }
        public bool RejectInsert { get; set; }
        public Task<MorningBrief?> GetAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult(Brief);
        public Task<bool> TryAddAsync(string phoneNumber, DateOnly date, string content, CancellationToken cancellationToken = default)
        {
            if (RejectInsert || Brief is not null) return Task.FromResult(false);
            Brief = new(phoneNumber, date, content, null);
            return Task.FromResult(true);
        }
        public Task<bool> TryMarkDeliveredAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default)
        {
            Brief = Brief! with { DeliveredAt = DateTimeOffset.UtcNow };
            return Task.FromResult(true);
        }
    }

    private sealed class Sender : IMessageSender
    {
        public int Texts { get; private set; }
        public int Templates { get; private set; }
        public IReadOnlyList<string>? BodyParameters { get; private set; }
        public bool Fail { get; set; }
        public Task SendTextAsync(string recipient, string text, CancellationToken cancellationToken = default)
        {
            Texts++;
            return Task.CompletedTask;
        }
        public Task SendTemplateAsync(string recipient, string templateName, string languageCode,
            IReadOnlyList<string>? bodyParameters = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal("morning_brief_ready", templateName);
            Assert.Equal("pt_BR", languageCode);
            Assert.NotNull(bodyParameters);
            Assert.Equal(2, bodyParameters.Count);
            Assert.Equal("Eduardo", bodyParameters[0]);
            BodyParameters = bodyParameters;
            Templates++;
            return Fail ? Task.FromException(new HttpRequestException("Timeout")) : Task.CompletedTask;
        }
    }
}

