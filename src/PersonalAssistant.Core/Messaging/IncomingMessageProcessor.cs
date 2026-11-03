using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Handles one received message: asks the assistant for a reply and sends it back.
/// </summary>
public sealed class IncomingMessageProcessor(
    IAssistantClient assistantClient,
    IMessageSender messageSender,
    ProcessedMessageTracker processedMessages)
{
    public const string FallbackReply =
        "Desculpa, tive um problema para responder agora. Tenta de novo daqui a pouco. 🙏";

    public async Task ProcessAsync(IncomingMessage message, CancellationToken cancellationToken = default)
    {
        if (!processedMessages.TryMarkAsProcessed(message.MessageId))
        {
            return;
        }

        string reply;

        try
        {
            reply = await assistantClient.GetReplyAsync(message.Text, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await messageSender.SendTextAsync(message.From, FallbackReply, cancellationToken);

            throw new MessageProcessingException(message.MessageId, exception);
        }

        await messageSender.SendTextAsync(message.From, reply, cancellationToken);
    }
}
