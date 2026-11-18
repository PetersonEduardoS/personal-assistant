using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Deadlines;
using PersonalAssistant.Core.Emails;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Notes;
using PersonalAssistant.Core.Shopping;
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
    INoteStore notes,
    IEmailInbox emails,
    IMorningBriefPreferenceStore briefPreferences,
    IShoppingListStore shopping,
    HouseholdNotifier household,
    IReceiptStore receipts)
{
    public IReadOnlyList<IAssistantTool> CreateFor(UserProfile user)
    {
        List<IAssistantTool> tools =
        [
            new AddAppointmentTool(appointments, user, household, clock, timeZone),
            new ListAppointmentsTool(appointments, user, clock, timeZone),
            new CancelAppointmentTool(appointments, user, household),
            new ListWorkShiftsTool(schedules, user, clock, timeZone),
            new ListDeadlinesTool(deadlines, user, clock, timeZone),
            new SaveNoteTool(notes, user.PhoneNumber),
            new ListNotesTool(notes, user.PhoneNumber),
            new DeleteNoteTool(notes, user.PhoneNumber),
            new SetMorningBriefTimeTool(briefPreferences, user.PhoneNumber)
        ];

        // Only users with a connected mailbox get the email tools.
        if (emails.IsConfigured(user))
        {
            tools.Add(new ListEmailsTool(emails, user, clock, timeZone));
            tools.Add(new ReadEmailTool(emails, user, timeZone));
        }

        // Only people who share a household get the shopping list.
        if (!string.IsNullOrWhiteSpace(user.Household))
        {
            tools.Add(new AddShoppingItemsTool(shopping, user));
            tools.Add(new ListShoppingTool(shopping, receipts, user));
            tools.Add(new MarkBoughtTool(shopping, user));
            tools.Add(new RemoveShoppingItemTool(shopping, user));
            tools.Add(new SaveReceiptTool(receipts, shopping, user, clock, timeZone));
            tools.Add(new EstimateShoppingTotalTool(shopping, receipts, user));
            tools.Add(new SpendingSummaryTool(receipts, user, clock, timeZone));
        }

        return tools;
    }
}
