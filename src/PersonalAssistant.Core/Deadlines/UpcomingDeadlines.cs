namespace PersonalAssistant.Core.Deadlines;

/// <summary>
/// The deadlines of the next days, or the reason they are not known.
/// </summary>
public sealed class UpcomingDeadlines
{
    /// <summary>How many days ahead the brief looks for deadlines, counting the day of the brief.</summary>
    public const int HorizonDays = 14;

    /// <summary>Deadlines up to this many days after the brief are highlighted.</summary>
    public const int UrgentDays = 3;

    private UpcomingDeadlines(UpcomingDeadlinesStatus status, IReadOnlyList<Deadline> deadlines)
    {
        Status = status;
        Deadlines = deadlines;
    }

    public static UpcomingDeadlines NotConfigured { get; } = new(UpcomingDeadlinesStatus.NotConfigured, []);

    public static UpcomingDeadlines Unavailable { get; } = new(UpcomingDeadlinesStatus.Unavailable, []);

    public UpcomingDeadlinesStatus Status { get; }

    public IReadOnlyList<Deadline> Deadlines { get; }

    public static UpcomingDeadlines WithDeadlines(IReadOnlyList<Deadline> deadlines) =>
        new(UpcomingDeadlinesStatus.Available, deadlines);
}
