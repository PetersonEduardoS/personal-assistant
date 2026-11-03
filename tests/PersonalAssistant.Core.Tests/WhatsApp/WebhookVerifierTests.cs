using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.WhatsApp;

public class WebhookVerifierTests
{
    private const string ExpectedToken = "expected-token";
    private const string Challenge = "1158201444";

    private readonly WebhookVerifier _verifier = new(ExpectedToken);

    [Fact]
    public void Verify_ReturnsChallenge_WhenModeIsSubscribeAndTokenMatches()
    {
        var result = _verifier.Verify("subscribe", ExpectedToken, Challenge);

        Assert.Equal(Challenge, result);
    }

    [Fact]
    public void Verify_ReturnsNull_WhenTokenDoesNotMatch()
    {
        var result = _verifier.Verify("subscribe", "wrong-token", Challenge);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unsubscribe")]
    public void Verify_ReturnsNull_WhenModeIsNotSubscribe(string? mode)
    {
        var result = _verifier.Verify(mode, ExpectedToken, Challenge);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_ReturnsNull_WhenTokenIsMissing(string? token)
    {
        var result = _verifier.Verify("subscribe", token, Challenge);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_ReturnsNull_WhenChallengeIsMissing(string? challenge)
    {
        var result = _verifier.Verify("subscribe", ExpectedToken, challenge);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_ReturnsNull_WhenExpectedTokenIsNotConfigured(string expectedToken)
    {
        var verifier = new WebhookVerifier(expectedToken);

        var result = verifier.Verify("subscribe", expectedToken, Challenge);

        Assert.Null(result);
    }
}
