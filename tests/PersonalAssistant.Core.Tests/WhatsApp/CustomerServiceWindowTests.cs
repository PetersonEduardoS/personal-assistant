using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.WhatsApp;

public class CustomerServiceWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsOpen_ReturnsFalse_WhenUserNeverSentMessage()
    {
        Assert.False(CustomerServiceWindow.IsOpen(lastUserMessageAt: null, Now));
    }

    [Fact]
    public void IsOpen_ReturnsTrue_WhenLastMessageIsLessThan24HoursOld()
    {
        var lastUserMessageAt = Now - TimeSpan.FromHours(23) - TimeSpan.FromMinutes(59);

        Assert.True(CustomerServiceWindow.IsOpen(lastUserMessageAt, Now));
    }

    [Fact]
    public void IsOpen_ReturnsFalse_WhenLastMessageIs24HoursOld()
    {
        var lastUserMessageAt = Now - TimeSpan.FromHours(24);

        Assert.False(CustomerServiceWindow.IsOpen(lastUserMessageAt, Now));
    }
}
