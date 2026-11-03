using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Handles one received message: saves it, asks the assistant for a reply
/// using the recent conversation, sends the reply and saves it too.
/// </summary>
public sealed class IncomingMessageProcessor(
    IAssistantClient assistantClient,
    IMessageSender messageSender,
    UserDirectory userDirectory,
    IConversationStore conversationStore)
{
    public const int HistoryLimit = 20;

    public const string FallbackReply =
        "Desculpa, tive um problema para responder agora. Tenta de novo daqui a pouco. 🙏";

    public async Task ProcessAsync(IncomingMessage message, CancellationToken cancellationToken = default)
    {
        var user = userDirectory.FindByPhoneNumber(message.From);

        if (user is null)
        {
            return;
        }

        var isNewMessage = await conversationStore.TryAddIncomingMessageAsync(
            message.From,
            message.MessageId,
            message.Text,
            message.Timestamp,
            cancellationToken);

        if (!isNewMessage)
        {
            return;
        }

        var conversation = await conversationStore.GetRecentTurnsAsync(message.From, HistoryLimit, cancellationToken);
        var request = new AssistantRequest(user.Name, conversation);

        string reply;

        try
        {
            reply = await assistantClient.GetReplyAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await messageSender.SendTextAsync(message.From, FallbackReply, cancellationToken);

            throw new MessageProcessingException(message.MessageId, exception);
        }

        await messageSender.SendTextAsync(message.From, reply, cancellationToken);
        await conversationStore.AddAssistantReplyAsync(message.From, reply, cancellationToken);
    }
}
