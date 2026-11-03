using PersonalAssistant.Core.Messaging;

namespace PersonalAssistant.Core.Tests.Messaging;

public class ProcessedMessageTrackerTests
{
    [Fact]
    public void TryMarkAsProcessed_ReturnsTrue_ForNewMessage()
    {
        var tracker = new ProcessedMessageTracker();

        Assert.True(tracker.TryMarkAsProcessed("wamid.TEST1"));
    }

    [Fact]
    public void TryMarkAsProcessed_ReturnsFalse_ForRepeatedMessage()
    {
        var tracker = new ProcessedMessageTracker();
        tracker.TryMarkAsProcessed("wamid.TEST1");

        Assert.False(tracker.TryMarkAsProcessed("wamid.TEST1"));
    }

    [Fact]
    public void TryMarkAsProcessed_ForgetsOldestMessage_WhenCapacityIsReached()
    {
        var tracker = new ProcessedMessageTracker(capacity: 2);
        tracker.TryMarkAsProcessed("wamid.TEST1");
        tracker.TryMarkAsProcessed("wamid.TEST2");
        tracker.TryMarkAsProcessed("wamid.TEST3");

        Assert.False(tracker.TryMarkAsProcessed("wamid.TEST3"));
        Assert.True(tracker.TryMarkAsProcessed("wamid.TEST1"));
    }
}
