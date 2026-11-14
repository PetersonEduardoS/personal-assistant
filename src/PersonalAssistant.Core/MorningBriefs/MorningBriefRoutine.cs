using PersonalAssistant.Core.WorkSchedules;
using SavedAppointment = PersonalAssistant.Core.Appointments.Appointment;

namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>Explicit routine data; travel times include the arrival margin, not live routing.</summary>
public sealed class MorningBriefRoutine
{
    public int PreparationMinutes { get; set; } = 30;
    public int WorkTravelMinutes { get; set; } = 25;

    /// <summary>Travel time for appointments saved from the chat, whose place is not known.</summary>
    public int AppointmentTravelMinutes { get; set; } = 25;
    public TimeOnly LatestTime { get; set; } = new(9, 0);
    public List<RoutineAppointment> Appointments { get; set; } = [];

    public void Validate()
    {
        if (PreparationMinutes is < 0 or > 240 || WorkTravelMinutes is < 0 or > 240 ||
            AppointmentTravelMinutes is < 0 or > 240 ||
            Appointments.Any(a => a.TravelMinutes is < 0 or > 240 ||
                (a.Date is null && (a.DayOfWeek is null || a.From is null || a.Until is null)) ||
                (a.DayOfWeek is { } day && !Enum.IsDefined(day)) ||
                (a.From is { } from && a.Until is { } until && until < from)))
            throw new InvalidOperationException("Invalid morning brief routine: check travel times and appointment dates.");
    }

    /// <summary>
    /// The earliest class, shift or saved appointment of the day decides the brief time,
    /// minus preparation and travel, capped at <see cref="LatestTime"/>.
    /// </summary>
    public TimeOnly GetTime(DateOnly date, WorkSchedule schedule, IReadOnlyList<SavedAppointment>? savedAppointments = null)
    {
        Validate();
        var candidates = Appointments.Where(a => a.OccursOn(date))
            .Select(a => a.Start.ToTimeSpan() - TimeSpan.FromMinutes(PreparationMinutes + a.TravelMinutes))
            .Concat(schedule.Shifts.Select(s => s.Start.ToTimeSpan() - TimeSpan.FromMinutes(PreparationMinutes + WorkTravelMinutes)))
            .Concat((savedAppointments ?? []).Where(a => a.Date == date)
                .Select(a => a.Time.ToTimeSpan() - TimeSpan.FromMinutes(PreparationMinutes + AppointmentTravelMinutes)));
        var time = candidates.Append(LatestTime.ToTimeSpan()).Min();
        // An early shift must not wrap the brief into the following evening.
        return TimeOnly.FromTimeSpan(time < TimeSpan.Zero ? TimeSpan.Zero : time);
    }
}

public sealed class RoutineAppointment
{
    public string Name { get; set; } = string.Empty;
    public TimeOnly Start { get; set; }
    public int TravelMinutes { get; set; }
    public DateOnly? Date { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? Until { get; set; }

    public bool OccursOn(DateOnly date) => Date is { } specific ? date == specific :
        DayOfWeek == date.DayOfWeek && From <= date && Until >= date;
}

public interface IMorningBriefRoutineProvider
{
    Task<MorningBriefRoutine?> GetAsync(Users.UserProfile user, CancellationToken cancellationToken = default);
}
