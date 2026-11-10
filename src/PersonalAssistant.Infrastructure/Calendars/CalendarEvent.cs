namespace PersonalAssistant.Infrastructure.Calendars;

/// <summary>
/// A timed event read from an iCalendar feed.
/// </summary>
public sealed record CalendarEvent(string Summary, DateTimeOffset Start, DateTimeOffset End);
