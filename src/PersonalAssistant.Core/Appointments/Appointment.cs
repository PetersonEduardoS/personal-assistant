namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// An appointment a user added from the chat, such as a haircut or a coffee with a friend.
/// The date and time are in Dublin, as the user said them.
/// <see cref="ReminderMinutesBefore"/> is how long before the appointment the reminder goes out,
/// or null when the user asked for no reminder. A shared appointment has the <see cref="Household"/>
/// of the couple and belongs to everyone in it; <see cref="CreatedBy"/> is the name of who booked it.
/// </summary>
public sealed record Appointment(
    long Id,
    string Title,
    DateOnly Date,
    TimeOnly Time,
    int? ReminderMinutesBefore = Appointment.DefaultReminderMinutes,
    string? Household = null,
    string? CreatedBy = null)
{
    public const int MaxTitleLength = 200;

    public const int DefaultReminderMinutes = 60;

    public const int MaxReminderMinutes = 24 * 60;

    public bool IsShared => Household is not null;
}
