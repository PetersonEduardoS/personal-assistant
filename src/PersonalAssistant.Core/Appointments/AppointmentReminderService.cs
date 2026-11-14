using System.Globalization;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// Sends the reminders that are due. A reminder is claimed before it is sent and is not retried,
/// because Meta may have accepted a send that failed on our side.
/// </summary>
public sealed class AppointmentReminderService(
    IAppointmentStore appointments,
    IConversationStore conversations,
    IMessageSender sender,
    TimeProvider clock,
    TimeZoneInfo timeZone)
{
    public const string TemplateName = "appointment_reminder";
    public const string TemplateLanguage = "pt_BR";

    /// <summary>Sends every reminder that is due now and returns how many were sent.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);

        // The longest reminder is one day, so only today and tomorrow can be due.
        var pending = await appointments.ListPendingRemindersAsync(today, today.AddDays(1), cancellationToken);
        var sent = 0;

        foreach (var reminder in pending)
        {
            var appointment = reminder.Appointment;
            var startsAt = ToUtc(appointment.Date.ToDateTime(appointment.Time));
            var remindAt = startsAt.AddMinutes(-appointment.ReminderMinutesBefore!.Value);

            if (now < remindAt || now >= startsAt || reminder.CreatedAt > remindAt)
            {
                continue;
            }

            if (!await appointments.TryMarkReminderSentAsync(appointment.Id, cancellationToken))
            {
                continue;
            }

            await SendAsync(reminder.PhoneNumber, appointment, today, startsAt - now, now, cancellationToken);
            sent++;
        }

        return sent;
    }

    private async Task SendAsync(
        string phoneNumber,
        Appointment appointment,
        DateOnly today,
        TimeSpan timeLeft,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var time = appointment.Time.ToString("HH:mm", CultureInfo.InvariantCulture);
        var lastMessage = await conversations.GetLastUserMessageAtAsync(phoneNumber, cancellationToken);
        string text;

        if (CustomerServiceWindow.IsOpen(lastMessage, now))
        {
            var day = appointment.Date == today ? "hoje" : "amanhã";
            var minutesLeft = Math.Max(1, (int)Math.Ceiling(timeLeft.TotalMinutes));
            text = $"⏰ Lembrete: {appointment.Title} {day} às {time} (daqui a {ReminderText.FormatDuration(minutesLeft)}).";
            await sender.SendTextAsync(phoneNumber, text, cancellationToken);
        }
        else
        {
            var date = appointment.Date.ToString("dd/MM", CultureInfo.InvariantCulture);
            await sender.SendTemplateAsync(phoneNumber, TemplateName, TemplateLanguage,
                [appointment.Title, date, time], cancellationToken);
            text = $"Lembrete do seu compromisso: {appointment.Title}, dia {date} às {time}. Responda se precisar de algo.";
        }

        // Kept in the conversation so the assistant understands a reply such as "obrigado, já estou indo".
        await conversations.AddAssistantReplyAsync(phoneNumber, text, cancellationToken);
    }

    private DateTimeOffset ToUtc(DateTime local)
    {
        // A time skipped by the spring clock change is read as one hour later.
        if (timeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
