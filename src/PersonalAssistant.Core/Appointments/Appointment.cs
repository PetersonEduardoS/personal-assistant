namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// An appointment a user added from the chat, such as a haircut or a coffee with a friend.
/// The date and time are in Dublin, as the user said them.
/// </summary>
public sealed record Appointment(long Id, string Title, DateOnly Date, TimeOnly Time)
{
    public const int MaxTitleLength = 200;
}
