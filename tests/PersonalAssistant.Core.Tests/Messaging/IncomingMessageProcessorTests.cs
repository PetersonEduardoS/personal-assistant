using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.Messaging;

public class IncomingMessageProcessorTests
{
    private const string Eduardo = "353800000001";

    private static readonly IncomingMessage Message =
        new("wamid.TEST1", Eduardo, "Oi", DateTimeOffset.UnixEpoch);

    private static readonly UserDirectory Users =
        new([new UserProfile { PhoneNumber = Eduardo, Name = "Eduardo" }]);

    private readonly FakeConversationStore _store = new();
    private readonly FakeMessageSender _sender = new();

    [Fact]
    public async Task ProcessAsync_SendsAssistantReply_ToSender()
    {
        var processor = CreateProcessor(new FakeAssistantClient("Olá!"));

        await processor.ProcessAsync(Message);

        var sent = Assert.Single(_sender.SentMessages);
        Assert.Equal((Eduardo, "Olá!"), sent);
    }

    [Fact]
    public async Task ProcessAsync_SendsUserNameAndConversationHistory_ToAssistant()
    {
        _store.Turns.Add((Eduardo, new ConversationTurn(ConversationRole.User, "Vai chover hoje?")));
        _store.Turns.Add((Eduardo, new ConversationTurn(ConversationRole.Assistant, "Sim, à tarde.")));
        var assistant = new FakeAssistantClient("Amanhã deve fazer sol.");
        var processor = CreateProcessor(assistant);

        await processor.ProcessAsync(Message with { Text = "E amanhã?" });

        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Equal("Eduardo", request.UserName);
        Assert.Equal(
            new[]
            {
                new ConversationTurn(ConversationRole.User, "Vai chover hoje?"),
                new ConversationTurn(ConversationRole.Assistant, "Sim, à tarde."),
                new ConversationTurn(ConversationRole.User, "E amanhã?")
            },
            request.Conversation);
    }

    [Fact]
    public async Task ProcessAsync_SavesAssistantReply_InConversation()
    {
        var processor = CreateProcessor(new FakeAssistantClient("Olá!"));

        await processor.ProcessAsync(Message);

        Assert.Equal(
            new[]
            {
                (Eduardo, new ConversationTurn(ConversationRole.User, "Oi")),
                (Eduardo, new ConversationTurn(ConversationRole.Assistant, "Olá!"))
            },
            _store.Turns);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresDuplicateMessage()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var processor = CreateProcessor(assistant);

        await processor.ProcessAsync(Message);
        await processor.ProcessAsync(Message);

        Assert.Single(assistant.ReceivedRequests);
        Assert.Single(_sender.SentMessages);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresMessage_WhenSenderIsUnknown()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var processor = CreateProcessor(assistant);

        await processor.ProcessAsync(Message with { From = "353899999999" });

        Assert.Empty(assistant.ReceivedRequests);
        Assert.Empty(_sender.SentMessages);
        Assert.Empty(_store.Turns);
    }

    [Fact]
    public async Task ProcessAsync_SendsFallbackWithoutSavingIt_WhenAssistantFails()
    {
        var processor = CreateProcessor(FakeAssistantClient.Failing());

        var exception = await Assert.ThrowsAsync<MessageProcessingException>(() => processor.ProcessAsync(Message));

        Assert.Equal("wamid.TEST1", exception.MessageId);
        var sent = Assert.Single(_sender.SentMessages);
        Assert.Equal((Eduardo, IncomingMessageProcessor.FallbackReply), sent);
        var stored = Assert.Single(_store.Turns);
        Assert.Equal(ConversationRole.User, stored.Turn.Role);
    }

    private IncomingMessageProcessor CreateProcessor(IAssistantClient assistant) =>
        new(assistant, _sender, Users, _store);
}
