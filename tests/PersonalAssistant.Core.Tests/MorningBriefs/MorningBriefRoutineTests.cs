using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Core.Tests.MorningBriefs;

public class MorningBriefRoutineTests
{
    private static MorningBriefRoutine Routine() => new()
    {
        Appointments =
        [
            new() { Name = "CCT", DayOfWeek = DayOfWeek.Wednesday, From = new(2026, 8, 24), Until = new(2026, 12, 31), Start = new(13, 30), TravelMinutes = 25 },
            new() { Name = "CCT", DayOfWeek = DayOfWeek.Thursday, From = new(2026, 8, 24), Until = new(2026, 12, 31), Start = new(8, 30), TravelMinutes = 25 },
            new() { Name = "LVA", Date = new(2026, 11, 17), Start = new(10, 0), TravelMinutes = 75 }
        ]
    };

    [Theory]
    [InlineData("2026-11-05", "07:35")]
    [InlineData("2026-11-04", "09:00")]
    [InlineData("2026-11-17", "08:15")]
    [InlineData("2026-11-10", "09:00")]
    [InlineData("2026-11-08", "09:00")]
    [InlineData("2027-01-07", "09:00")]
    [InlineData("2026-08-20", "09:00")]
    public void GetTime_UsesExactDatesBoundedWeeklyClassesAndFreeDayFallback(string date, string expected)
    {
        Assert.Equal(TimeOnly.Parse(expected), Routine().GetTime(DateOnly.Parse(date), WorkSchedule.NotConfigured));
    }

    [Theory]
    [InlineData("2026-11-05", "08:00", "07:05")]
    [InlineData("2026-11-05", "12:00", "07:35")]
    [InlineData("2026-11-06", "08:30", "07:35")]
    [InlineData("2026-11-06", "15:00", "09:00")]
    [InlineData("2026-11-06", "00:20", "00:00")]
    [InlineData("2026-11-17", "09:00", "08:05")]
    public void GetTime_WorkAndClassesCompeteForEarliestTime(string date, string shift, string expected)
    {
        var schedule = WorkSchedule.WithShifts([new(TimeOnly.Parse(shift), new(20, 0), "Work")]);
        Assert.Equal(TimeOnly.Parse(expected), Routine().GetTime(DateOnly.Parse(date), schedule));
    }

    [Fact]
    public void GetTime_UsesEarliestShiftRegardlessOfOrdering()
    {
        var schedule = WorkSchedule.WithShifts([new(new(18, 0), new(23, 0), "Late"), new(new(8, 0), new(12, 0), "Early")]);
        Assert.Equal(new TimeOnly(7, 5), Routine().GetTime(new(2026, 11, 6), schedule));
    }

    [Fact]
    public void GetTime_UnavailableScheduleStillUsesKnownClasses()
    {
        Assert.Equal(new TimeOnly(7, 35), Routine().GetTime(new(2026, 11, 5), WorkSchedule.Unavailable));
    }

    [Theory]
    [InlineData("08:30", "07:35")]
    [InlineData("15:00", "09:00")]
    [InlineData("07:00", "06:05")]
    public void GetTime_ChatAppointmentsCompeteForEarliestTime(string appointmentTime, string expected)
    {
        // Saturday 14 November: no class and no shift.
        var saturday = new DateOnly(2026, 11, 14);
        Appointment[] saved = [new(1, "Café", saturday, TimeOnly.Parse(appointmentTime))];

        Assert.Equal(TimeOnly.Parse(expected), Routine().GetTime(saturday, WorkSchedule.NotConfigured, saved));
    }

    [Fact]
    public void GetTime_UsesTheConfiguredTravelTime_ForChatAppointments()
    {
        var saturday = new DateOnly(2026, 11, 14);
        var routine = Routine();
        routine.AppointmentTravelMinutes = 10;
        Appointment[] saved = [new(1, "Café", saturday, new TimeOnly(8, 30))];

        Assert.Equal(new TimeOnly(7, 50), routine.GetTime(saturday, WorkSchedule.NotConfigured, saved));
    }

    [Fact]
    public void Validate_RejectsNegativeAppointmentTravelTime()
    {
        var routine = new MorningBriefRoutine { AppointmentTravelMinutes = -1 };
        Assert.Throws<InvalidOperationException>(routine.Validate);
    }

    [Fact]
    public void Validate_RejectsUnboundedWeeklyClasses()
    {
        var routine = new MorningBriefRoutine { Appointments = [new() { DayOfWeek = DayOfWeek.Thursday }] };
        Assert.Throws<InvalidOperationException>(routine.Validate);
    }

    [Fact]
    public void Validate_RejectsNegativeTravelTime()
    {
        var routine = new MorningBriefRoutine { WorkTravelMinutes = -1 };
        Assert.Throws<InvalidOperationException>(routine.Validate);
    }
}
