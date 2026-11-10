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

        var entry = new AppointmentEntry
        {
            PhoneNumber = phoneNumber,
            Title = cleanTitle,
            Date = date,
            Time = time,
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
        CancellationToken cancellationToken = default)
    {
        var entries = await context.Appointments
            .AsNoTracking()
            .Where(entry => entry.PhoneNumber == phoneNumber && entry.Date >= fromDate && entry.Date <= toDate)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Time)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return entries.Select(ToAppointment).ToList();
    }

    public async Task<Appointment?> CancelAsync(
        string phoneNumber,
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        var entry = await context.Appointments
            .SingleOrDefaultAsync(
                appointment => appointment.Id == appointmentId && appointment.PhoneNumber == phoneNumber,
                cancellationToken);

        if (entry is null)
        {
            return null;
        }

        context.Appointments.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);

        return ToAppointment(entry);
    }

    private static Appointment ToAppointment(AppointmentEntry entry) =>
        new(entry.Id, entry.Title, entry.Date, entry.Time);
}
