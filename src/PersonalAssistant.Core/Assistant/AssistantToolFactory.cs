using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Deadlines;
using PersonalAssistant.Core.Notes;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// Creates the tools the assistant can use in a conversation, bound to that user,
/// so the assistant can only see and change that user's data.
/// </summary>
public sealed class AssistantToolFactory(
    IAppointmentStore appointments,
    IWorkScheduleProvider schedules,
    IDeadlineProvider deadlines,
    TimeProvider clock,
    TimeZoneInfo timeZone,
    INoteStore notes)
{
    public IReadOnlyList<IAssistantTool> CreateFor(UserProfile user) =>
    [
        new AddAppointmentTool(appointments, user.PhoneNumber, clock, timeZone),
        new ListAppointmentsTool(appointments, user.PhoneNumber, clock, timeZone),
        new CancelAppointmentTool(appointments, user.PhoneNumber),
        new ListWorkShiftsTool(schedules, user, clock, timeZone),
        new ListDeadlinesTool(deadlines, user, clock, timeZone),
        new SaveNoteTool(notes, user.PhoneNumber),
        new ListNotesTool(notes, user.PhoneNumber),
        new DeleteNoteTool(notes, user.PhoneNumber)
    ];
}
