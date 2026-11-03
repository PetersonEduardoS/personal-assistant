using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.Messaging;

public class IncomingMessageProcessorTests
{
    private static readonly IncomingMessage Message =
        new("wamid.TEST1", "353800000001", "Oi", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task ProcessAsync_SendsAssistantReply_ToSender()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var sender = new FakeMessageSender();
        var processor = new IncomingMessageProcessor(assistant, sender, new ProcessedMessageTracker());

        await processor.ProcessAsync(Message);

        Assert.Equal(new[] { "Oi" }, assistant.ReceivedMessages);
        var sent = Assert.Single(sender.SentMessages);
        Assert.Equal(("353800000001", "Olá!"), sent);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresDuplicateMessage()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var sender = new FakeMessageSender();
        var processor = new IncomingMessageProcessor(assistant, sender, new ProcessedMessageTracker());

        await processor.ProcessAsync(Message);
        await processor.ProcessAsync(Message);

        Assert.Single(assistant.ReceivedMessages);
        Assert.Single(sender.SentMessages);
    }

    [Fact]
    public async Task ProcessAsync_SendsFallbackAndThrows_WhenAssistantFails()
    {
        var sender = new FakeMessageSender();
        var processor = new IncomingMessageProcessor(
            FakeAssistantClient.Failing(),
            sender,
            new ProcessedMessageTracker());

        var exception = await Assert.ThrowsAsync<MessageProcessingException>(() => processor.ProcessAsync(Message));

        Assert.Equal("wamid.TEST1", exception.MessageId);
        var sent = Assert.Single(sender.SentMessages);
        Assert.Equal(("353800000001", IncomingMessageProcessor.FallbackReply), sent);
    }
}
