using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.Messaging;

public class IncomingMessageProcessorTests
{
    private static readonly IncomingMessage Message =
        new("wamid.TEST1", "353800000001", "Oi", DateTimeOffset.UnixEpoch);

    private static readonly UserDirectory Users =
        new([new UserProfile { PhoneNumber = "353800000001", Name = "Eduardo" }]);

    [Fact]
    public async Task ProcessAsync_SendsAssistantReply_ToSender()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var sender = new FakeMessageSender();
        var processor = CreateProcessor(assistant, sender);

        await processor.ProcessAsync(Message);

        var sent = Assert.Single(sender.SentMessages);
        Assert.Equal(("353800000001", "Olá!"), sent);
    }

    [Fact]
    public async Task ProcessAsync_SendsUserNameAndMessage_ToAssistant()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var processor = CreateProcessor(assistant, new FakeMessageSender());

        await processor.ProcessAsync(Message);

        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Equal("Eduardo", request.UserName);
        var turn = Assert.Single(request.Conversation);
        Assert.Equal(ConversationRole.User, turn.Role);
        Assert.Equal("Oi", turn.Content);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresDuplicateMessage()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var sender = new FakeMessageSender();
        var processor = CreateProcessor(assistant, sender);

        await processor.ProcessAsync(Message);
        await processor.ProcessAsync(Message);

        Assert.Single(assistant.ReceivedRequests);
        Assert.Single(sender.SentMessages);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresMessage_WhenSenderIsUnknown()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var sender = new FakeMessageSender();
        var processor = CreateProcessor(assistant, sender);
        var unknownMessage = Message with { From = "353899999999" };

        await processor.ProcessAsync(unknownMessage);

        Assert.Empty(assistant.ReceivedRequests);
        Assert.Empty(sender.SentMessages);
    }

    [Fact]
    public async Task ProcessAsync_SendsFallbackAndThrows_WhenAssistantFails()
    {
        var sender = new FakeMessageSender();
        var processor = CreateProcessor(FakeAssistantClient.Failing(), sender);

        var exception = await Assert.ThrowsAsync<MessageProcessingException>(() => processor.ProcessAsync(Message));

        Assert.Equal("wamid.TEST1", exception.MessageId);
        var sent = Assert.Single(sender.SentMessages);
        Assert.Equal(("353800000001", IncomingMessageProcessor.FallbackReply), sent);
    }

    private static IncomingMessageProcessor CreateProcessor(IAssistantClient assistant, IMessageSender sender) =>
        new(assistant, sender, Users, new ProcessedMessageTracker());
}
