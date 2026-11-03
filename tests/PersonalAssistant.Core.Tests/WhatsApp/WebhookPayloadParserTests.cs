using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.WhatsApp;

public class WebhookPayloadParserTests
{
    private readonly WebhookPayloadParser _parser = new();

    [Fact]
    public void Parse_ReturnsTextMessage_WhenPayloadHasTextMessage()
    {
        var messages = _parser.Parse(SamplePayloads.TextMessage);

        var message = Assert.Single(messages);
        Assert.Equal("wamid.TEST1", message.MessageId);
        Assert.Equal("353800000001", message.From);
        Assert.Equal("Oi, tudo bem?", message.Text);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1762200000), message.Timestamp);
    }

    [Fact]
    public void Parse_ReturnsAllTextMessages_WhenPayloadHasSeveral()
    {
        var messages = _parser.Parse(SamplePayloads.TwoTextMessages);

        Assert.Equal(new[] { "wamid.TEST1", "wamid.TEST2" }, messages.Select(message => message.MessageId));
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenPayloadHasOnlyStatuses()
    {
        var messages = _parser.Parse(SamplePayloads.StatusOnly);

        Assert.Empty(messages);
    }

    [Fact]
    public void Parse_IgnoresMessages_ThatAreNotText()
    {
        var messages = _parser.Parse(SamplePayloads.ImageMessage);

        Assert.Empty(messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Parse_ReturnsEmpty_WhenJsonIsInvalidOrEmpty(string json)
    {
        var messages = _parser.Parse(json);

        Assert.Empty(messages);
    }
}
