using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.Appointments;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class EfAppointmentStore(AssistantDbContext context, TimeProvider timeProvider) : IAppointmentStore
{
    public async Task<Appointment> AddAsync(
        string phoneNumber,
        string title,
        DateOnly date,
        TimeOnly time,
        int? reminderMinutesBefore = Appointment.DefaultReminderMinutes,
        string? household = null,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        var cleanTitle = title.Trim();

        if (cleanTitle.Length == 0)
        {
            throw new ArgumentException("The appointment needs a title.", nameof(title));
        }

        if (cleanTitle.Length > Appointment.MaxTitleLength)
        {
            throw new ArgumentException(
                $"The appointment title can have at most {Appointment.MaxTitleLength} characters.",
                nameof(title));
        }

        if (reminderMinutesBefore is < 1 or > Appointment.MaxReminderMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reminderMinutesBefore),
                $"The reminder must be between 1 and {Appointment.MaxReminderMinutes} minutes before.");
        }

        var entry = new AppointmentEntry
        {
            PhoneNumber = phoneNumber,
            Title = cleanTitle,
            Date = date,
            Time = time,
            ReminderMinutesBefore = reminderMinutesBefore,
            Household = string.IsNullOrWhiteSpace(household) ? null : household,
            CreatedBy = createdBy,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        context.Appointments.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return ToAppointment(entry);
    }

    public async Task<IReadOnlyList<Appointment>> ListAsync(
        string phoneNumber,
        DateOnly fromDate,
        DateOnly toDate,
        string? household = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await Visible(phoneNumber, household)
            .AsNoTracking()
            .Where(entry => entry.Date >= fromDate && entry.Date <= toDate)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Time)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return entries.Select(ToAppointment).ToList();
    }

    public async Task<Appointment?> CancelAsync(
        string phoneNumber,
        long appointmentId,
        string? household = null,
        CancellationToken cancellationToken = default)
    {
        var entry = await Visible(phoneNumber, household)
            .SingleOrDefaultAsync(appointment => appointment.Id == appointmentId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        context.Appointments.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);

        return ToAppointment(entry);
    }

    public async Task<IReadOnlyList<PendingReminder>> ListPendingRemindersAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var entries = await context.Appointments
            .AsNoTracking()
            .Where(entry => entry.ReminderMinutesBefore != null
                && entry.ReminderSentAtUtc == null
                && entry.Date >= fromDate
                && entry.Date <= toDate)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Time)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return entries
            .Select(entry => new PendingReminder(
                entry.PhoneNumber,
                ToAppointment(entry),
                UtcDateTimes.ToOffset(entry.CreatedAtUtc)))
            .ToList();
    }

    public async Task<bool> TryMarkReminderSentAsync(long appointmentId, CancellationToken cancellationToken = default)
    {
        DateTime? sentAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        // A single UPDATE that only changes an unsent reminder, so only one caller can win.
        var updatedRows = await context.Appointments
            .Where(entry => entry.Id == appointmentId && entry.ReminderSentAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(entry => entry.ReminderSentAtUtc, sentAtUtc),
                cancellationToken);

        return updatedRows == 1;
    }

    /// <summary>The person's own appointments plus the shared appointments of their household.</summary>
    private IQueryable<AppointmentEntry> Visible(string phoneNumber, string? household) =>
        string.IsNullOrWhiteSpace(household)
            ? context.Appointments.Where(entry => entry.PhoneNumber == phoneNumber)
            : context.Appointments.Where(entry => entry.PhoneNumber == phoneNumber || entry.Household == household);

    private static Appointment ToAppointment(AppointmentEntry entry) =>
        new(entry.Id, entry.Title, entry.Date, entry.Time, entry.ReminderMinutesBefore, entry.Household, entry.CreatedBy);
}
