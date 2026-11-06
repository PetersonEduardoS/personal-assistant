namespace PersonalAssistant.Core.WorkSchedules;

/// <summary>
/// A user's work shifts on one day, or the reason they are not known.
/// </summary>
public sealed class WorkSchedule
{
    private WorkSchedule(WorkScheduleStatus status, IReadOnlyList<WorkShift> shifts)
    {
        Status = status;
        Shifts = shifts;
    }

    public static WorkSchedule NotConfigured { get; } = new(WorkScheduleStatus.NotConfigured, []);

    public static WorkSchedule Unavailable { get; } = new(WorkScheduleStatus.Unavailable, []);

    public WorkScheduleStatus Status { get; }

    public IReadOnlyList<WorkShift> Shifts { get; }

    public static WorkSchedule WithShifts(IReadOnlyList<WorkShift> shifts) => new(WorkScheduleStatus.Available, shifts);
}
