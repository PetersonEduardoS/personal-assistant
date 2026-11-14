namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// An appointment whose reminder has not been sent yet, with its owner and when it was saved.
/// </summary>
public sealed record PendingReminder(string PhoneNumber, Appointment Appointment, DateTimeOffset CreatedAt);
