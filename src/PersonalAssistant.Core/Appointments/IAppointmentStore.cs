namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// Keeps the appointments of each user. A user only sees and cancels their own appointments.
/// </summary>
public interface IAppointmentStore
{
    /// <summary>
    /// Saves an appointment. The title is trimmed; an empty or too long title, or a reminder outside
    /// 1 to <see cref="Appointment.MaxReminderMinutes"/> minutes, throws <see cref="ArgumentException"/>.
    /// A null reminder means no reminder.
    /// </summary>
    Task<Appointment> AddAsync(
        string phoneNumber,
        string title,
        DateOnly date,
        TimeOnly time,
        int? reminderMinutesBefore = Appointment.DefaultReminderMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the appointments from <paramref name="fromDate"/> to <paramref name="toDate"/> (both included),
    /// in date and time order.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListAsync(
        string phoneNumber,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the appointment and returns it, or returns null when it does not exist or belongs to someone else.
    /// </summary>
    Task<Appointment?> CancelAsync(string phoneNumber, long appointmentId, CancellationToken cancellationToken = default);
}
