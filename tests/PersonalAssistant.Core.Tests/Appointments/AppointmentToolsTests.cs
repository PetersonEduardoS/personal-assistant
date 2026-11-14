using System.Text.Json;
using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Appointments;

public class AppointmentToolsTests
{
    // Tuesday, 10 November 2026, 23:00 in Dublin.
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 23, 0, 0, TimeSpan.Zero);

    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo" };
    private static readonly UserProfile Karina = new() { PhoneNumber = "353800000002", Name = "Karina" };

    private readonly FakeAppointmentStore _store = new();

    [Fact]
    public void CreateFor_ReturnsAllTheTools_WithValidSchemas()
    {
        var tools = CreateFactory().CreateFor(Eduardo);

        Assert.Equal(
            new[] { "add_appointment", "list_appointments", "cancel_appointment", "list_work_shifts", "list_deadlines" },
            tools.Select(tool => tool.Name));
        Assert.All(tools, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            using var schema = JsonDocument.Parse(tool.InputSchema);
            Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        });
    }

    [Fact]
    public void AddDescription_AsksForTheCleanConfirmationFormat()
    {
        var add = Tool(Eduardo, "add_appointment");

        Assert.Contains("Título dia dd/MM às HH:mm", add.Description);
        Assert.Contains("pergunte", add.Description);
    }

    [Fact]
    public async Task Add_SavesTheAppointment_AndReturnsTheCleanFormat()
    {
        var result = await Run(Eduardo, "add_appointment",
            """{ "title": "Corte de cabelo", "date": "2026-11-13", "time": "13:30" }""");

        Assert.Contains("Corte de cabelo dia 13/11 às 13:30", result);
        var saved = Assert.Single(_store.Appointments);
        Assert.Equal(Eduardo.PhoneNumber, saved.PhoneNumber);
        Assert.Equal(new DateOnly(2026, 11, 13), saved.Appointment.Date);
        Assert.Equal(new TimeOnly(13, 30), saved.Appointment.Time);
    }

    [Fact]
    public async Task Add_SetsAOneHourReminder_ByDefault_AndSaysSo()
    {
        var result = await Run(Eduardo, "add_appointment",
            """{ "title": "Corte de cabelo", "date": "2026-11-13", "time": "13:30" }""");

        Assert.Contains("lembrete 1 hora antes", result);
        Assert.Equal(60, Assert.Single(_store.Appointments).Appointment.ReminderMinutesBefore);
    }

    [Theory]
    [InlineData(120, "lembrete 2 horas antes", 120)]
    [InlineData(30, "lembrete 30 minutos antes", 30)]
    [InlineData(90, "lembrete 1 hora e 30 minutos antes", 90)]
    [InlineData(0, "sem lembrete", null)]
    public async Task Add_UsesTheChosenReminder(int minutesBefore, string expectedText, int? expectedStored)
    {
        var result = await Run(Eduardo, "add_appointment",
            $$"""{ "title": "Dentista", "date": "2026-11-13", "time": "09:00", "remind_minutes_before": {{minutesBefore}} }""");

        Assert.Contains(expectedText, result);
        Assert.Equal(expectedStored, Assert.Single(_store.Appointments).Appointment.ReminderMinutesBefore);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(1441)]
    public async Task Add_RejectsAReminderOutOfRange(int minutesBefore)
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "add_appointment",
            $$"""{ "title": "Dentista", "date": "2026-11-13", "time": "09:00", "remind_minutes_before": {{minutesBefore}} }"""));
    }

    [Fact]
    public async Task Add_RejectsADateAndTimeInThePast()
    {
        var exception = await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "add_appointment",
            """{ "title": "Corte de cabelo", "date": "2026-10-13", "time": "14:30" }"""));

        Assert.Contains("já passou", exception.Message);
        Assert.Empty(_store.Appointments);
    }

    [Theory]
    [InlineData("13/11/2026", "13:30")]
    [InlineData("2026-11-13", "1:30pm")]
    [InlineData("2026-02-30", "13:30")]
    public async Task Add_RejectsInvalidDatesAndTimes(string date, string time)
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "add_appointment",
            $$"""{ "title": "Corte de cabelo", "date": "{{date}}", "time": "{{time}}" }"""));

        Assert.Empty(_store.Appointments);
    }

    [Fact]
    public async Task Add_RejectsAnEmptyTitle()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "add_appointment",
            """{ "title": "  ", "date": "2026-11-13", "time": "13:30" }"""));
    }

    [Fact]
    public async Task Add_RejectsAMissingField()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "add_appointment",
            """{ "title": "Corte de cabelo", "date": "2026-11-13" }"""));
    }

    [Fact]
    public async Task List_ReturnsTheAppointmentsOfThePeriod_WithTheirIds()
    {
        await _store.AddAsync(Eduardo.PhoneNumber, "Corte de cabelo", new DateOnly(2026, 11, 13), new TimeOnly(13, 30));
        await _store.AddAsync(Eduardo.PhoneNumber, "Café com o Marivaldo", new DateOnly(2026, 11, 20), new TimeOnly(12, 0));
        await _store.AddAsync(Eduardo.PhoneNumber, "Dentista", new DateOnly(2026, 12, 20), new TimeOnly(9, 0));

        var result = await Run(Eduardo, "list_appointments", """{ "from_date": "2026-11-11", "to_date": "2026-11-30" }""");

        Assert.Contains("- Corte de cabelo dia 13/11 às 13:30 (id 1)", result);
        Assert.Contains("- Café com o Marivaldo dia 20/11 às 12:00 (id 2)", result);
        Assert.DoesNotContain("Dentista", result);
    }

    [Fact]
    public async Task List_UsesTheNext30Days_WhenNoPeriodIsGiven()
    {
        await _store.AddAsync(Eduardo.PhoneNumber, "Corte de cabelo", new DateOnly(2026, 11, 13), new TimeOnly(13, 30));
        await _store.AddAsync(Eduardo.PhoneNumber, "Dentista", new DateOnly(2027, 1, 20), new TimeOnly(9, 0));

        var result = await Run(Eduardo, "list_appointments", "{}");

        Assert.Contains("Corte de cabelo", result);
        Assert.DoesNotContain("Dentista", result);
    }

    [Fact]
    public async Task List_SaysWhenThereIsNothing()
    {
        var result = await Run(Eduardo, "list_appointments", """{ "from_date": "2026-11-11", "to_date": "2026-11-12" }""");

        Assert.Contains("Nenhum compromisso", result);
    }

    [Fact]
    public async Task List_RejectsPeriodsLongerThanTheLimit()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "list_appointments",
            """{ "from_date": "2026-11-11", "to_date": "2027-03-01" }"""));
    }

    [Fact]
    public async Task Cancel_RemovesTheAppointment_AndConfirms()
    {
        await _store.AddAsync(Eduardo.PhoneNumber, "Corte de cabelo", new DateOnly(2026, 11, 13), new TimeOnly(13, 30));

        var result = await Run(Eduardo, "cancel_appointment", """{ "appointment_id": 1 }""");

        Assert.Contains("Cancelado: Corte de cabelo dia 13/11 às 13:30", result);
        Assert.Empty(_store.Appointments);
    }

    [Fact]
    public async Task Cancel_RejectsAnUnknownId()
    {
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Eduardo, "cancel_appointment", """{ "appointment_id": 42 }"""));
    }

    [Fact]
    public async Task Tools_OnlySeeAndCancelTheAppointmentsOfTheirUser()
    {
        await _store.AddAsync(Eduardo.PhoneNumber, "Corte de cabelo", new DateOnly(2026, 11, 13), new TimeOnly(13, 30));

        var listed = await Run(Karina, "list_appointments", """{ "from_date": "2026-11-11", "to_date": "2026-11-30" }""");

        Assert.DoesNotContain("Corte de cabelo", listed);
        await Assert.ThrowsAsync<AssistantToolException>(() => Run(Karina, "cancel_appointment", """{ "appointment_id": 1 }"""));
        Assert.Single(_store.Appointments);
    }

    private AssistantToolFactory CreateFactory() =>
        new(_store, new FakeWorkScheduleProvider(), new FakeDeadlineProvider(),
            new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin"));

    private IAssistantTool Tool(UserProfile user, string name) =>
        CreateFactory().CreateFor(user).Single(tool => tool.Name == name);

    private async Task<string> Run(UserProfile user, string name, string input)
    {
        using var document = JsonDocument.Parse(input);

        return await Tool(user, name).ExecuteAsync(document.RootElement);
    }
}
