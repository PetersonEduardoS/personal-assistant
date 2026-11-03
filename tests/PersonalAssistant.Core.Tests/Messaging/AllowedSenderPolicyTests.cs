using PersonalAssistant.Core.Messaging;

namespace PersonalAssistant.Core.Tests.Messaging;

public class AllowedSenderPolicyTests
{
    [Fact]
    public void IsAllowed_ReturnsTrue_WhenSenderIsInTheList()
    {
        var policy = new AllowedSenderPolicy(["353800000001", "353800000002"]);

        Assert.True(policy.IsAllowed("353800000002"));
    }

    [Fact]
    public void IsAllowed_ReturnsFalse_WhenSenderIsNotInTheList()
    {
        var policy = new AllowedSenderPolicy(["353800000001"]);

        Assert.False(policy.IsAllowed("353899999999"));
    }

    [Theory]
    [InlineData("+353800000001")]
    [InlineData("+353 80 000 0001")]
    [InlineData(" 353800000001 ")]
    public void IsAllowed_IgnoresPlusSignAndSpacesInConfiguredNumbers(string configuredNumber)
    {
        var policy = new AllowedSenderPolicy([configuredNumber]);

        Assert.True(policy.IsAllowed("353800000001"));
    }

    [Fact]
    public void IsAllowed_ReturnsFalse_WhenListIsEmpty()
    {
        var policy = new AllowedSenderPolicy([]);

        Assert.False(policy.IsAllowed("353800000001"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsAllowed_ReturnsFalse_WhenSenderIsMissing(string? sender)
    {
        var policy = new AllowedSenderPolicy(["353800000001"]);

        Assert.False(policy.IsAllowed(sender));
    }
}
