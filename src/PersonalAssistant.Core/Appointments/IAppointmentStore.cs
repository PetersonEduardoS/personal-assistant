namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// Keeps the appointments of each user. A user sees and cancels their own appointments and the shared
/// appointments of their household; personal appointments of other people stay private.
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
        string? household = null,
        string? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the appointments from <paramref name="fromDate"/> to <paramref name="toDate"/> (both included),
    /// in date and time order.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListAsync(
        string phoneNumber,
        DateOnly fromDate,
        DateOnly toDate,
        string? household = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the appointment and returns it, or returns null when it does not exist or belongs to someone else.
    /// </summary>
    Task<Appointment?> CancelAsync(
        string phoneNumber,
        long appointmentId,
        string? household = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns, for every user, the appointments from <paramref name="fromDate"/> to <paramref name="toDate"/>
    /// that have a reminder not sent yet.
    /// </summary>
    Task<IReadOnlyList<PendingReminder>> ListPendingRemindersAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims the reminder before it is sent. Returns false when it was already claimed or the appointment is gone,
    /// so a reminder is never sent twice.
    /// </summary>
    Task<bool> TryMarkReminderSentAsync(long appointmentId, CancellationToken cancellationToken = default);
}
