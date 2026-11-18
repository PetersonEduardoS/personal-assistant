using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Core.MorningBriefs;

public enum MorningBriefResult { NotDue, AlreadyPrepared, TextSent, TemplateSent }

/// <summary>
/// Prepares and claims one brief per day before attempting an external send.
/// A failed or interrupted send is not retried automatically, because Meta may have accepted it.
/// </summary>
public sealed class MorningBriefScheduler(
    MorningBriefWriter writer,
    IWorkScheduleProvider schedules,
    IMorningBriefStore briefs,
    IConversationStore conversations,
    IMessageSender sender,
    TimeProvider clock,
    TimeZoneInfo timeZone,
    IMorningBriefRoutineProvider routines,
    MorningBriefDelivery delivery,
    IAppointmentStore appointments,
    IMorningBriefPreferenceStore preferences)
{
    public const string TemplateName = "morning_brief_ready";
    public const string TemplateLanguage = "pt_BR";
    public static readonly TimeSpan CatchUpWindow = TimeSpan.FromHours(1);

    public Task<MorningBriefResult> RunAsync(UserProfile user, CancellationToken cancellationToken = default) =>
        RunCoreAsync(user, false, cancellationToken);

    public Task<MorningBriefResult> RunNowAsync(UserProfile user, CancellationToken cancellationToken = default) =>
        RunCoreAsync(user, true, cancellationToken);

    private async Task<MorningBriefResult> RunCoreAsync(UserProfile user, bool manual, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).DateTime);
        // A choice made in the chat wins: "off" stops the brief, a fixed time replaces the routine.
        var preference = manual ? null : await preferences.GetAsync(user.PhoneNumber, cancellationToken);
        if (preference is { Enabled: false }) return MorningBriefResult.NotDue;

        var routine = manual || preference?.Time is not null ? null : await routines.GetAsync(user, cancellationToken);
        WorkSchedule? schedule = null;
        var scheduled = preference?.Time ?? user.MorningBriefTime;
        if (routine is not null)
        {
            // No schedule lookup is needed once even the latest catch-up window has closed.
            var latestEnd = routine.LatestTime.ToTimeSpan() + CatchUpWindow;
            var localTime = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).TimeOfDay;
            if (localTime >= latestEnd) return MorningBriefResult.NotDue;
            schedule = await schedules.GetScheduleAsync(user, date, cancellationToken);
            var savedAppointments = await appointments.ListAsync(user.PhoneNumber, date, date, user.Household, cancellationToken);
            scheduled = routine.GetTime(date, schedule, savedAppointments);
        }
        if (!manual && !IsDue(scheduled, clock.GetUtcNow())) return MorningBriefResult.NotDue;

        if (await briefs.GetAsync(user.PhoneNumber, date, cancellationToken) is not null)
            return MorningBriefResult.AlreadyPrepared;

        schedule ??= await schedules.GetScheduleAsync(user, date, cancellationToken);
        var content = await writer.WriteAsync(user, date, schedule, cancellationToken);
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("The morning brief is empty.");

        // Generation can take time: never send a stale brief after the catch-up window or midnight.
        var now = clock.GetUtcNow();
        if ((!manual && !IsDue(scheduled, now)) || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime) != date)
            return MorningBriefResult.NotDue;

        if (!await briefs.TryAddAsync(user.PhoneNumber, date, content, cancellationToken))
            return MorningBriefResult.AlreadyPrepared;

        var lastMessage = await conversations.GetLastUserMessageAtAsync(user.PhoneNumber, cancellationToken);
        if (CustomerServiceWindow.IsOpen(lastMessage, clock.GetUtcNow()))
        {
            return await delivery.TryDeliverAsync(user.PhoneNumber, date, cancellationToken)
                ? MorningBriefResult.TextSent : MorningBriefResult.AlreadyPrepared;
        }

        await sender.SendTemplateAsync(user.PhoneNumber, TemplateName, TemplateLanguage,
            [user.Name, PortugueseDates.FormatShort(date)], cancellationToken);
        return MorningBriefResult.TemplateSent;
    }

    private bool IsDue(TimeOnly? scheduledTime, DateTimeOffset now)
    {
        if (scheduledTime is not { } scheduled) return false;
        var local = TimeZoneInfo.ConvertTime(now, timeZone);
        var elapsed = TimeOnly.FromDateTime(local.DateTime).ToTimeSpan() - scheduled.ToTimeSpan();
        return elapsed >= TimeSpan.Zero && elapsed < CatchUpWindow;
    }
}
