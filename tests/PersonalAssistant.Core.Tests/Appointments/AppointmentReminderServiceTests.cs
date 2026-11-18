using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Appointments;

public class AppointmentReminderServiceTests
{
    private const string Eduardo = "353800000001";

    // Saturday, 14 November 2026, 10:00 in Dublin (UTC in winter).
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 14);

    private readonly FakeAppointmentStore _store = new() { CreatedAt = Now.AddDays(-2) };
    private readonly FakeConversationStore _conversations = new();
    private readonly RecordingSender _sender = new();

    [Fact]
    public async Task RunAsync_SendsAText_WhenTheWindowIsOpen()
    {
        await OpenWindow();
        var appointment = await _store.AddAsync(Eduardo, "Corte de cabelo", Today, new TimeOnly(11, 0));

        var sent = await CreateService().RunAsync();

        Assert.Equal(1, sent);
        var message = Assert.Single(_sender.Texts);
        Assert.Equal(Eduardo, message.Recipient);
        Assert.Equal("⏰ Lembrete: Corte de cabelo hoje às 11:00 (daqui a 1 hora).", message.Text);
        Assert.Empty(_sender.Templates);
        Assert.Contains(appointment.Id, _store.SentReminders);
        Assert.Contains(_conversations.Turns, turn => turn.Turn.Role == ConversationRole.Assistant
            && turn.Turn.Content == message.Text);
    }

    [Fact]
    public async Task RunAsync_SendsTheTemplate_WhenTheWindowIsClosed()
    {
        await _store.AddAsync(Eduardo, "Corte de cabelo", Today, new TimeOnly(11, 0));

        var sent = await CreateService().RunAsync();

        Assert.Equal(1, sent);
        Assert.Empty(_sender.Texts);
        var template = Assert.Single(_sender.Templates);
        Assert.Equal(Eduardo, template.Recipient);
        Assert.Equal("appointment_reminder", template.Name);
        Assert.Equal("pt_BR", template.Language);
        Assert.Equal(new[] { "Corte de cabelo", "14/11", "11:00" }, template.Parameters.ToArray());
        Assert.Contains(_conversations.Turns, turn => turn.Turn.Role == ConversationRole.Assistant
            && turn.Turn.Content.Contains("Corte de cabelo"));
    }

    [Fact]
    public async Task RunAsync_NeverSendsTheSameReminderTwice()
    {
        await OpenWindow();
        await _store.AddAsync(Eduardo, "Corte de cabelo", Today, new TimeOnly(11, 0));
        var service = CreateService();

        await service.RunAsync();
        var sentAgain = await service.RunAsync();

        Assert.Equal(0, sentAgain);
        Assert.Single(_sender.Texts);
    }

    [Fact]
    public async Task RunAsync_WaitsUntilTheReminderIsDue()
    {
        await OpenWindow();
        await _store.AddAsync(Eduardo, "Corte de cabelo", Today, new TimeOnly(11, 1));

        Assert.Equal(0, await CreateService().RunAsync());
        Assert.Empty(_sender.Texts);
        Assert.Empty(_store.SentReminders);
    }

    [Fact]
    public async Task RunAsync_StillSendsALateReminder_BeforeTheAppointmentStarts()
    {
        // The app was down at 09:30; the appointment has not started yet.
        await OpenWindow();
        await _store.AddAsync(Eduardo, "Dentista", Today, new TimeOnly(10, 30));

        await CreateService().RunAsync();

        Assert.Equal("⏰ Lembrete: Dentista hoje às 10:30 (daqui a 30 minutos).", Assert.Single(_sender.Texts).Text);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(9, 30)]
    public async Task RunAsync_SkipsAnAppointmentThatAlreadyStarted(int hour, int minute)
    {
        await OpenWindow();
        await _store.AddAsync(Eduardo, "Dentista", Today, new TimeOnly(hour, minute));

        Assert.Equal(0, await CreateService().RunAsync());
        Assert.Empty(_sender.Texts);
    }

    [Fact]
    public async Task RunAsync_SkipsAnAppointmentCreatedInsideTheReminderWindow()
    {
        // Saved at 09:45 for 10:30: the person just talked about it, no reminder needed.
        await OpenWindow();
        _store.CreatedAt = Now.AddMinutes(-15);
        await _store.AddAsync(Eduardo, "Dentista", Today, new TimeOnly(10, 30));

        Assert.Equal(0, await CreateService().RunAsync());
        Assert.Empty(_sender.Texts);
    }

    [Fact]
    public async Task RunAsync_SkipsAnAppointmentWithoutReminder()
    {
        await OpenWindow();
        await _store.AddAsync(Eduardo, "Corte de cabelo", Today, new TimeOnly(11, 0), reminderMinutesBefore: null);

        Assert.Equal(0, await CreateService().RunAsync());
        Assert.Empty(_sender.Texts);
    }

    [Fact]
    public async Task RunAsync_SaysTomorrow_ForAReminderTheDayBefore()
    {
        await OpenWindow();
        await _store.AddAsync(Eduardo, "Café com a Karina", Today.AddDays(1), new TimeOnly(10, 0), 24 * 60);

        await CreateService().RunAsync();

        Assert.Equal("⏰ Lembrete: Café com a Karina amanhã às 10:00 (daqui a 24 horas).", Assert.Single(_sender.Texts).Text);
    }

    [Theory]
    [InlineData("2026-07-06T09:00:00Z", true)]
    [InlineData("2026-07-06T08:59:00Z", false)]
    public async Task RunAsync_UsesDublinTime_InSummer(string timestamp, bool due)
    {
        // 11:00 in Dublin in July is 10:00 UTC, so a one-hour reminder is due at 09:00 UTC.
        var now = DateTimeOffset.Parse(timestamp);
        await _conversations.TryAddIncomingMessageAsync(Eduardo, "wamid.1", "Oi", now.AddMinutes(-5));
        _store.CreatedAt = now.AddDays(-1);
        await _store.AddAsync(Eduardo, "Dentista", new DateOnly(2026, 7, 6), new TimeOnly(11, 0));

        var sent = await CreateService(now).RunAsync();

        Assert.Equal(due ? 1 : 0, sent);
    }

    private Task OpenWindow() =>
        _conversations.TryAddIncomingMessageAsync(Eduardo, "wamid.1", "Oi", Now.AddHours(-1));

    [Fact]
    public async Task RunAsync_RemindsEveryoneInTheHousehold_ForASharedAppointment()
    {
        await OpenWindow();
        await _store.AddAsync(Eduardo, "Jantar fora", Today, new TimeOnly(11, 0), household: "casa", createdBy: "Eduardo");

        var sent = await CreateService().RunAsync();

        Assert.Equal(1, sent);
        Assert.Equal(
            (Eduardo, "⏰ Lembrete pros dois: Jantar fora hoje às 11:00 (daqui a 1 hora)."),
            Assert.Single(_sender.Texts));
        var template = Assert.Single(_sender.Templates);
        Assert.Equal(Karina, template.Recipient);
        Assert.Equal(new[] { "Jantar fora", "14/11", "11:00" }, template.Parameters.ToArray());
    }

    private const string Karina = "353800000002";

    private AppointmentReminderService CreateService(DateTimeOffset? now = null) => new(
        _store,
        _conversations,
        _sender,
        new FixedTimeProvider(now ?? Now),
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"),
        new UserDirectory(
        [
            new UserProfile { PhoneNumber = Eduardo, Name = "Eduardo", Household = "casa" },
            new UserProfile { PhoneNumber = Karina, Name = "Karina", Household = "casa" }
        ]));

    private sealed class RecordingSender : IMessageSender
    {
        public List<(string Recipient, string Text)> Texts { get; } = [];

        public List<(string Recipient, string Name, string Language, IReadOnlyList<string> Parameters)> Templates { get; } = [];

        public Task SendTextAsync(string recipient, string text, CancellationToken cancellationToken = default)
        {
            Texts.Add((recipient, text));
            return Task.CompletedTask;
        }

        public Task SendTemplateAsync(string recipient, string templateName, string languageCode,
            IReadOnlyList<string>? bodyParameters = null, CancellationToken cancellationToken = default)
        {
            Templates.Add((recipient, templateName, languageCode, bodyParameters ?? []));
            return Task.CompletedTask;
        }
    }
}
