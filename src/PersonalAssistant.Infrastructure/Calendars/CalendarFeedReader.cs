using System.Globalization;
using System.Text;

namespace PersonalAssistant.Infrastructure.Calendars;

/// <summary>
/// Reads the events of an iCalendar (.ics) feed. It supports what scheduling apps such as Findmyshift use:
/// timed events in UTC or with a TZID, folded lines and escaped text. All-day events, such as days off,
/// are skipped.
/// </summary>
public static class CalendarFeedReader
{
    private const string DateTimeFormat = "yyyyMMdd'T'HHmmss";
    private const int DateOnlyLength = 8;

    public static IReadOnlyList<CalendarEvent> Read(string calendar)
    {
        var events = new List<CalendarEvent>();
        Dictionary<string, CalendarProperty>? currentEvent = null;
        var nestedComponents = 0;

        foreach (var line in Unfold(calendar))
        {
            if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase))
            {
                if (IsEventMarker(line))
                {
                    currentEvent = new Dictionary<string, CalendarProperty>(StringComparer.OrdinalIgnoreCase);
                    nestedComponents = 0;
                }
                else if (currentEvent is not null)
                {
                    // A component inside the event, such as an alarm. Its properties are not the event's.
                    nestedComponents++;
                }

                continue;
            }

            if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
            {
                if (IsEventMarker(line))
                {
                    if (currentEvent is not null && TryCreateEvent(currentEvent, out var calendarEvent))
                    {
                        events.Add(calendarEvent);
                    }

                    currentEvent = null;
                }
                else if (nestedComponents > 0)
                {
                    nestedComponents--;
                }

                continue;
            }

            if (currentEvent is null || nestedComponents > 0)
            {
                continue;
            }

            var property = ParseProperty(line);

            if (property is not null)
            {
                currentEvent.TryAdd(property.Name, property);
            }
        }

        return events;
    }

    private static bool IsEventMarker(string line) =>
        line.EndsWith(":VEVENT", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Long lines are split into several lines; a line that starts with a space or a tab continues the previous one.
    /// </summary>
    private static IEnumerable<string> Unfold(string calendar)
    {
        var lines = calendar.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var current = new StringBuilder();
        var hasCurrent = false;

        foreach (var line in lines)
        {
            if (hasCurrent && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                current.Append(line, 1, line.Length - 1);
                continue;
            }

            if (hasCurrent)
            {
                yield return current.ToString();
            }

            current.Clear().Append(line);
            hasCurrent = true;
        }

        if (hasCurrent)
        {
            yield return current.ToString();
        }
    }

    private static CalendarProperty? ParseProperty(string line)
    {
        var colon = line.IndexOf(':');

        if (colon <= 0)
        {
            return null;
        }

        var nameAndParameters = line[..colon].Split(';');
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in nameAndParameters.Skip(1))
        {
            var parts = parameter.Split('=', 2);

            if (parts.Length == 2)
            {
                parameters[parts[0]] = parts[1].Trim('"');
            }
        }

        return new CalendarProperty(nameAndParameters[0], parameters, line[(colon + 1)..]);
    }

    private static bool TryCreateEvent(
        Dictionary<string, CalendarProperty> properties,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CalendarEvent? calendarEvent)
    {
        calendarEvent = null;

        if (!properties.TryGetValue("DTSTART", out var start)
            || !properties.TryGetValue("DTEND", out var end)
            || IsAllDay(start)
            || IsAllDay(end))
        {
            return false;
        }

        var summary = properties.TryGetValue("SUMMARY", out var summaryProperty)
            ? Unescape(summaryProperty.Value).Trim()
            : string.Empty;

        var categories = properties.TryGetValue("CATEGORIES", out var categoriesProperty)
            ? Unescape(categoriesProperty.Value).Trim()
            : string.Empty;

        calendarEvent = new CalendarEvent(summary, ParseDateTime(start), ParseDateTime(end), categories);

        return true;
    }

    private static bool IsAllDay(CalendarProperty property) =>
        (property.Parameters.TryGetValue("VALUE", out var valueType)
            && valueType.Equals("DATE", StringComparison.OrdinalIgnoreCase))
        || property.Value.Trim().Length == DateOnlyLength;

    private static DateTimeOffset ParseDateTime(CalendarProperty property)
    {
        var value = property.Value.Trim();

        if (value.EndsWith('Z'))
        {
            var utc = DateTime.ParseExact(value[..^1], DateTimeFormat, CultureInfo.InvariantCulture);

            return new DateTimeOffset(utc, TimeSpan.Zero);
        }

        if (property.Parameters.TryGetValue("TZID", out var timeZoneId))
        {
            var local = DateTime.ParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
        }

        throw new FormatException("Calendar times without a time zone are not supported.");
    }

    private static string Unescape(string text)
    {
        var result = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\' && index + 1 < text.Length)
            {
                index++;
                result.Append(text[index] is 'n' or 'N' ? '\n' : text[index]);
            }
            else
            {
                result.Append(text[index]);
            }
        }

        return result.ToString();
    }

    private sealed record CalendarProperty(string Name, IReadOnlyDictionary<string, string> Parameters, string Value);
}
