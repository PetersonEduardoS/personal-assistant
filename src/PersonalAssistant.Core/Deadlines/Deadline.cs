namespace PersonalAssistant.Core.Deadlines;

/// <summary>
/// A college deadline, such as an assignment or a quiz closing. <see cref="DueAt"/> is in Dublin time.
/// </summary>
public sealed record Deadline(string Title, string? Course, DateTimeOffset DueAt);
