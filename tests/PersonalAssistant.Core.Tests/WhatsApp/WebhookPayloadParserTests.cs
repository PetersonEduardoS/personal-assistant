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
    public void Parse_ReturnsAnImage_WithItsMedia()
    {
        var message = Assert.Single(_parser.Parse(SamplePayloads.ImageMessage));

        Assert.Equal(IncomingMessageKind.Image, message.Kind);
        Assert.Equal(new MessageAttachment("300000000000000", "image/jpeg", null), message.Attachment);
        Assert.Equal(string.Empty, message.Text);
        Assert.Equal("wamid.TEST3", message.MessageId);
    }

    [Fact]
    public void Parse_UsesTheCaption_AsTheText()
    {
        var message = Assert.Single(_parser.Parse(SamplePayloads.ImageWithCaption));

        Assert.Equal("Salva essa consulta", message.Text);
        Assert.Equal("image/png", message.Attachment!.MimeType);
    }

    [Fact]
    public void Parse_ReturnsADocument_WithItsFileName()
    {
        var message = Assert.Single(_parser.Parse(SamplePayloads.DocumentMessage));

        Assert.Equal(IncomingMessageKind.Document, message.Kind);
        Assert.Equal(new MessageAttachment("400000000000000", "application/pdf", "CA2 brief.pdf"), message.Attachment);
        Assert.Equal("Resume pf", message.Text);
    }

    [Fact]
    public void Parse_MarksAnAudio_AsUnsupported()
    {
        var message = Assert.Single(_parser.Parse(SamplePayloads.AudioMessage));

        Assert.Equal(IncomingMessageKind.Unsupported, message.Kind);
        Assert.Equal("audio", message.UnsupportedType);
        Assert.Null(message.Attachment);
    }

    [Fact]
    public void Parse_IgnoresReactions_AndMediaWithoutId()
    {
        Assert.Empty(_parser.Parse(SamplePayloads.ReactionMessage));
        Assert.Empty(_parser.Parse(SamplePayloads.ImageWithoutId));
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
