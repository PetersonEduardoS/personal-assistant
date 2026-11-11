using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// Creates the appointment tools for one user, so the assistant can only touch that user's appointments.
/// </summary>
public sealed class AppointmentToolFactory(IAppointmentStore store, TimeProvider clock, TimeZoneInfo timeZone)
{
    public IReadOnlyList<IAssistantTool> CreateFor(UserProfile user) =>
    [
        new AddAppointmentTool(store, user.PhoneNumber, clock, timeZone),
        new ListAppointmentsTool(store, user.PhoneNumber, clock, timeZone),
        new CancelAppointmentTool(store, user.PhoneNumber)
    ];
}
