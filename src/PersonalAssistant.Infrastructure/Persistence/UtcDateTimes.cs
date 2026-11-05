namespace PersonalAssistant.Infrastructure.Persistence;

internal static class UtcDateTimes
{
    /// <summary>
    /// SQLite returns dates without a time zone. They are always saved in UTC, so they are read back as UTC.
    /// </summary>
    public static DateTimeOffset ToOffset(DateTime utcDateTime) =>
        new(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
}
