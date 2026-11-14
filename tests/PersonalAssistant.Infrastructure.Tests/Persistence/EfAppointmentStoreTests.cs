using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class EfAppointmentStoreTests : IDisposable
{
    private const string Eduardo = "353800000001";
    private const string Karina = "353800000002";

    private static readonly DateOnly Friday = new(2026, 11, 13);

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task AddAsync_StoresTheAppointment_AndReturnsItWithAnId()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        var added = await store.AddAsync(Eduardo, "Corte de cabelo", Friday, new TimeOnly(13, 30));

        Assert.True(added.Id > 0);
        var listed = Assert.Single(await store.ListAsync(Eduardo, Friday, Friday));
        Assert.Equal(new Appointment(added.Id, "Corte de cabelo", Friday, new TimeOnly(13, 30)), listed);
    }

    [Fact]
    public async Task AddAsync_StoresAOneHourReminder_ByDefault()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        var added = await store.AddAsync(Eduardo, "Corte de cabelo", Friday, new TimeOnly(13, 30));

        Assert.Equal(60, added.ReminderMinutesBefore);
        Assert.Equal(60, Assert.Single(await store.ListAsync(Eduardo, Friday, Friday)).ReminderMinutesBefore);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(null)]
    public async Task AddAsync_StoresTheChosenReminder(int? minutesBefore)
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        await store.AddAsync(Eduardo, "Dentista", Friday, new TimeOnly(9, 0), minutesBefore);

        Assert.Equal(minutesBefore, Assert.Single(await store.ListAsync(Eduardo, Friday, Friday)).ReminderMinutesBefore);
    }

    [Fact]
    public async Task ListPendingRemindersAsync_ReturnsUnsentReminders_OfEveryUser_InTheRange()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        var haircut = await store.AddAsync(Eduardo, "Corte de cabelo", Friday, new TimeOnly(13, 30));
        var nails = await store.AddAsync(Karina, "Unhas", Friday.AddDays(1), new TimeOnly(10, 0), 120);
        await store.AddAsync(Eduardo, "Sem lembrete", Friday, new TimeOnly(15, 0), null);
        await store.AddAsync(Eduardo, "Fora do intervalo", Friday.AddDays(2), new TimeOnly(9, 0));

        var pending = await store.ListPendingRemindersAsync(Friday, Friday.AddDays(1));

        Assert.Equal(new[] { haircut.Id, nails.Id }, pending.Select(reminder => reminder.Appointment.Id).Order().ToArray());
        var first = pending.Single(reminder => reminder.Appointment.Id == haircut.Id);
        Assert.Equal(Eduardo, first.PhoneNumber);
        Assert.Equal(_clock.GetUtcNow(), first.CreatedAt);
        Assert.Equal(Karina, pending.Single(reminder => reminder.Appointment.Id == nails.Id).PhoneNumber);
    }

    [Fact]
    public async Task TryMarkReminderSentAsync_WinsOnlyOnce_AndHidesTheReminder()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        var added = await store.AddAsync(Eduardo, "Corte de cabelo", Friday, new TimeOnly(13, 30));

        Assert.True(await store.TryMarkReminderSentAsync(added.Id));
        Assert.False(await store.TryMarkReminderSentAsync(added.Id));
        Assert.Empty(await store.ListPendingRemindersAsync(Friday, Friday));
        Assert.Single(await store.ListAsync(Eduardo, Friday, Friday));
    }

    [Fact]
    public async Task TryMarkReminderSentAsync_ReturnsFalse_ForAnUnknownAppointment()
    {
        await using var context = _database.CreateContext();

        Assert.False(await CreateStore(context).TryMarkReminderSentAsync(999));
    }

    [Fact]
    public async Task AddAsync_TrimsTheTitle()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        var added = await store.AddAsync(Eduardo, "  Café com o Marivaldo  ", Friday, new TimeOnly(12, 0));

        Assert.Equal("Café com o Marivaldo", added.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddAsync_Throws_WhenTheTitleIsEmpty(string title)
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        await Assert.ThrowsAsync<ArgumentException>(() => store.AddAsync(Eduardo, title, Friday, new TimeOnly(12, 0)));
    }

    [Fact]
    public async Task AddAsync_Throws_WhenTheTitleIsTooLong()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        var title = new string('a', Appointment.MaxTitleLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() => store.AddAsync(Eduardo, title, Friday, new TimeOnly(12, 0)));
    }

    [Fact]
    public async Task ListAsync_ReturnsTheAppointmentsOfTheRange_InDateAndTimeOrder()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        await store.AddAsync(Eduardo, "Depois do intervalo", Friday.AddDays(2), new TimeOnly(9, 0));
        await store.AddAsync(Eduardo, "Jantar", Friday.AddDays(1), new TimeOnly(20, 0));
        await store.AddAsync(Eduardo, "Café", Friday.AddDays(1), new TimeOnly(9, 0));
        await store.AddAsync(Eduardo, "Corte de cabelo", Friday, new TimeOnly(13, 30));
        await store.AddAsync(Eduardo, "Antes do intervalo", Friday.AddDays(-1), new TimeOnly(9, 0));

        var appointments = await store.ListAsync(Eduardo, Friday, Friday.AddDays(1));

        Assert.Equal(new[] { "Corte de cabelo", "Café", "Jantar" }, appointments.Select(appointment => appointment.Title));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyTheAppointmentsOfThatPerson()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        await store.AddAsync(Eduardo, "Corte de cabelo", Friday, new TimeOnly(13, 30));
        await store.AddAsync(Karina, "Unhas", Friday, new TimeOnly(10, 0));

        var appointments = await store.ListAsync(Karina, Friday, Friday);

        Assert.Equal("Unhas", Assert.Single(appointments).Title);
    }

    [Fact]
    public async Task CancelAsync_RemovesTheAppointment_AndReturnsIt()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        var added = await store.AddAsync(Eduardo, "Corte de cabelo", Friday, new TimeOnly(13, 30));

        var cancelled = await store.CancelAsync(Eduardo, added.Id);

        Assert.Equal(added, cancelled);
        Assert.Empty(await store.ListAsync(Eduardo, Friday, Friday));
    }

    [Fact]
    public async Task CancelAsync_ReturnsNull_AndKeepsIt_WhenTheAppointmentBelongsToSomeoneElse()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        var added = await store.AddAsync(Karina, "Unhas", Friday, new TimeOnly(10, 0));

        var cancelled = await store.CancelAsync(Eduardo, added.Id);

        Assert.Null(cancelled);
        Assert.Single(await store.ListAsync(Karina, Friday, Friday));
    }

    [Fact]
    public async Task CancelAsync_ReturnsNull_WhenTheAppointmentDoesNotExist()
    {
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        Assert.Null(await store.CancelAsync(Eduardo, 42));
    }

    private EfAppointmentStore CreateStore(AssistantDbContext context) => new(context, _clock);
}
