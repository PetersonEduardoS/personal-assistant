namespace PersonalAssistant.Infrastructure.Tests;

/// <summary>
/// A clock that returns the time the test sets, so tests do not depend on the real time.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
