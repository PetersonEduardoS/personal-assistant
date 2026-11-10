namespace PersonalAssistant.Infrastructure.Calendars;

/// <summary>
/// A timed event read from an iCalendar feed.
/// </summary>
/// <param name="Categories">The raw CATEGORIES text. Moodle puts the course name there.</param>
public sealed record CalendarEvent(string Summary, DateTimeOffset Start, DateTimeOffset End, string Categories = "");
