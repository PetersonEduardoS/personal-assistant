using PersonalAssistant.Infrastructure.Calendars;

namespace PersonalAssistant.Infrastructure.Tests.Calendars;

public class CalendarFeedReaderTests
{
    [Fact]
    public void Read_ReturnsTimedEvents_WithUtcTimes()
    {
        var calendar = CalendarFeeds.Calendar(
            CalendarFeeds.Event("DTSTART:20261106T170000Z", "DTEND:20261107T010000Z", "SUMMARY:BAR"));

        var calendarEvent = Assert.Single(CalendarFeedReader.Read(calendar));

        Assert.Equal("BAR", calendarEvent.Summary);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 17, 0, 0, TimeSpan.Zero), calendarEvent.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 1, 0, 0, TimeSpan.Zero), calendarEvent.End);
    }

    [Fact]
    public void Read_SkipsAllDayEvents()
    {
        var calendar = CalendarFeeds.Calendar(
            CalendarFeeds.Event("DTSTART;VALUE=DATE:20261107", "DTEND;VALUE=DATE:20261108", "SUMMARY:OFF"),
            CalendarFeeds.Event("DTSTART:20261108T133000Z", "DTEND:20261108T210000Z", "SUMMARY:BAR"));

        var calendarEvent = Assert.Single(CalendarFeedReader.Read(calendar));

        Assert.Equal(new DateTimeOffset(2026, 11, 8, 13, 30, 0, TimeSpan.Zero), calendarEvent.Start);
    }

    [Fact]
    public void Read_JoinsFoldedLines()
    {
        var calendar =
            "BEGIN:VCALENDAR\r\n" +
            "BEGIN:VEVENT\r\n" +
            "DTSTART:20261106T170000Z\r\n" +
            "DTEND:20261107T010000Z\r\n" +
            "SUMMARY:BAR AND\r\n" +
            "  RESTAURANT\r\n" +
            "END:VEVENT\r\n" +
            "END:VCALENDAR\r\n";

        var calendarEvent = Assert.Single(CalendarFeedReader.Read(calendar));

        Assert.Equal("BAR AND RESTAURANT", calendarEvent.Summary);
    }

    [Fact]
    public void Read_UnescapesText()
    {
        var calendar = CalendarFeeds.Calendar(
            CalendarFeeds.Event("DTSTART:20261106T170000Z", "DTEND:20261107T010000Z", @"SUMMARY:BAR\, FLOOR\; CLOSE"));

        var calendarEvent = Assert.Single(CalendarFeedReader.Read(calendar));

        Assert.Equal("BAR, FLOOR; CLOSE", calendarEvent.Summary);
    }

    [Fact]
    public void Read_ReadsTimesWithTimeZoneId()
    {
        var calendar = CalendarFeeds.Calendar(
            CalendarFeeds.Event(
                "DTSTART;TZID=Europe/Dublin:20260706T110000",
                "DTEND;TZID=Europe/Dublin:20260706T160000",
                "SUMMARY:BAR"));

        var calendarEvent = Assert.Single(CalendarFeedReader.Read(calendar));

        // Dublin is one hour ahead of UTC in summer.
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero), calendarEvent.Start.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero), calendarEvent.End.ToUniversalTime());
    }

    [Fact]
    public void Read_IgnoresPropertiesOfNestedComponents()
    {
        var calendar = CalendarFeeds.Calendar(
            CalendarFeeds.Event(
                "DTSTART:20261106T170000Z",
                "DTEND:20261107T010000Z",
                "SUMMARY:BAR",
                "BEGIN:VALARM",
                "SUMMARY:Reminder",
                "TRIGGER:-PT30M",
                "END:VALARM"));

        var calendarEvent = Assert.Single(CalendarFeedReader.Read(calendar));

        Assert.Equal("BAR", calendarEvent.Summary);
    }

    [Fact]
    public void Read_ReturnsNoEvents_WhenCalendarIsEmpty()
    {
        Assert.Empty(CalendarFeedReader.Read(CalendarFeeds.Calendar()));
    }

    [Fact]
    public void Read_Throws_ForTimesWithoutTimeZone()
    {
        var calendar = CalendarFeeds.Calendar(
            CalendarFeeds.Event("DTSTART:20261106T170000", "DTEND:20261107T010000", "SUMMARY:BAR"));

        Assert.Throws<FormatException>(() => CalendarFeedReader.Read(calendar));
    }
}
