using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Appointments;

public class SharedAppointmentsTests
{
    // Wednesday, 18 November 2026, 10:00 in Dublin.
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo", Household = "casa" };
    private static readonly UserProfile Karina = new() { PhoneNumber = "353800000002", Name = "Karina", Household = "casa" };
    private static readonly UserProfile Alone = new() { PhoneNumber = "353800000003", Name = "Sozinho" };

    private const string Dinner = """{ "title": "Jantar fora", "date": "2026-11-20", "time": "20:00", "shared": true }""";

    private readonly FakeAppointmentStore _store = new();
    private readonly FakeConversationStore _conversations = new();
    private readonly FakeMessageSender _sender = new();

    [Fact]
    public async Task Add_Shared_SavesItForTheHousehold_AndSaysItIsForBoth()
    {
        var result = await Run(Eduardo, "add_appointment", Dinner);

        var saved = Assert.Single(_store.Appointments).Appointment;
        Assert.Equal("casa", saved.Household);
        Assert.Equal("Eduardo", saved.CreatedBy);
        Assert.Contains("Jantar fora dia 20/11 às 20:00 (pros dois, lembrete 1 hora antes)", result);
    }

    [Fact]
    public async Task Add_Personal_IsNotShared_AndNotifiesNobody()
    {
        await OpenWindow(Karina);

        await Run(Eduardo, "add_appointment", """{ "title": "Dentista", "date": "2026-11-20", "time": "09:00" }""");

        Assert.Null(Assert.Single(_store.Appointments).Appointment.Household);
        Assert.Empty(_sender.SentMessages);
    }

    [Fact]
    public async Task Add_Shared_IsRefused_ForSomeoneWithoutAHousehold()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Alone, "add_appointment", Dinner));
        Assert.Empty(_store.Appointments);
    }

    [Fact]
    public async Task Add_Shared_NotifiesThePartner_WhenHerWindowIsOpen()
    {
        await OpenWindow(Karina);

        var result = await Run(Eduardo, "add_appointment", Dinner);

        var notice = Assert.Single(_sender.SentMessages);
        Assert.Equal(Karina.PhoneNumber, notice.Recipient);
        Assert.Equal("📅 Eduardo marcou pros dois: Jantar fora dia 20/11 às 20:00.", notice.Text);
        Assert.Contains(_conversations.Turns, turn => turn.PhoneNumber == Karina.PhoneNumber
            && turn.Turn.Role == ConversationRole.Assistant && turn.Turn.Content == notice.Text);
        Assert.Contains("Avisei: Karina", result);
    }

    [Fact]
    public async Task Add_Shared_DoesNotNotify_WhenThePartnersWindowIsClosed()
    {
        var result = await Run(Eduardo, "add_appointment", Dinner);

        Assert.Empty(_sender.SentMessages);
        Assert.Contains("Karina não recebeu aviso agora", result);
    }

    [Fact]
    public async Task List_ShowsSharedAppointmentsToBoth_ButKeepsPersonalOnesPrivate()
    {
        await _store.AddAsync(Eduardo.PhoneNumber, "Jantar fora", new DateOnly(2026, 11, 20), new TimeOnly(20, 0),
            household: "casa", createdBy: "Eduardo");
        await _store.AddAsync(Eduardo.PhoneNumber, "Dentista", new DateOnly(2026, 11, 20), new TimeOnly(9, 0));

        var listed = await Run(Karina, "list_appointments", """{ "from_date": "2026-11-18", "to_date": "2026-11-30" }""");

        Assert.Contains("Jantar fora dia 20/11 às 20:00 (pros dois, marcado por Eduardo)", listed);
        Assert.DoesNotContain("Dentista", listed);
    }

    [Fact]
    public async Task Cancel_Shared_ByThePartner_RemovesIt_AndNotifiesWhoBookedIt()
    {
        var dinner = await _store.AddAsync(Eduardo.PhoneNumber, "Jantar fora", new DateOnly(2026, 11, 20), new TimeOnly(20, 0),
            household: "casa", createdBy: "Eduardo");
        await OpenWindow(Eduardo);

        var result = await Run(Karina, "cancel_appointment", $$"""{ "appointment_id": {{dinner.Id}} }""");

        Assert.Empty(_store.Appointments);
        Assert.Contains("Cancelado: Jantar fora dia 20/11 às 20:00", result);
        Assert.Equal((Eduardo.PhoneNumber, "🗑️ Karina cancelou o compromisso de vocês dois: Jantar fora dia 20/11 às 20:00."),
            Assert.Single(_sender.SentMessages));
    }

    [Fact]
    public async Task Cancel_CannotRemoveThePartnersPersonalAppointment()
    {
        var dentist = await _store.AddAsync(Eduardo.PhoneNumber, "Dentista", new DateOnly(2026, 11, 20), new TimeOnly(9, 0));

        await Assert.ThrowsAsync<AssistantToolException>(() =>
            Run(Karina, "cancel_appointment", $$"""{ "appointment_id": {{dentist.Id}} }"""));
        Assert.Single(_store.Appointments);
    }

    private Task OpenWindow(UserProfile user) =>
        _conversations.TryAddIncomingMessageAsync(user.PhoneNumber, $"wamid.{user.Name}", "Oi", Now.AddHours(-1));

    private async Task<string> Run(UserProfile user, string name, string input)
    {
        var clock = new FixedTimeProvider(Now);
        var factory = new AssistantToolFactory(
            _store, new FakeWorkScheduleProvider(), new FakeDeadlineProvider(), clock,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"), new FakeNoteStore(), new FakeEmailInbox(),
            new FakeMorningBriefPreferenceStore(), new FakeShoppingListStore(),
            TestHousehold.Notifier([Eduardo, Karina, Alone], _conversations, _sender, clock), new FakeReceiptStore());

        using var document = JsonDocument.Parse(input);

        return await factory.CreateFor(user).Single(tool => tool.Name == name).ExecuteAsync(document.RootElement);
    }
}
