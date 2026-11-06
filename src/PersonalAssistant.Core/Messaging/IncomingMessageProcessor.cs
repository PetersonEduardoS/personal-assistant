using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Handles one received message: saves it, asks the assistant for a reply
/// using the recent conversation and the user's context, sends the reply and saves it too.
/// Messages from unknown numbers, repeated messages and messages that are too old are ignored.
/// </summary>
public sealed class IncomingMessageProcessor(
    IAssistantClient assistantClient,
    IMessageSender messageSender,
    UserDirectory userDirectory,
    IConversationStore conversationStore,
    IUserContextProvider userContextProvider,
    TimeProvider timeProvider,
    MorningBriefDelivery briefDelivery,
    TimeZoneInfo timeZone)
{
    public const int HistoryLimit = 20;

    /// <summary>
    /// Meta retries webhooks that failed, sometimes hours later. A reply to a message
    /// older than this would arrive out of context, so the message is skipped.
    /// </summary>
    public static readonly TimeSpan MaxMessageAge = TimeSpan.FromMinutes(10);

    public const string FallbackReply =
        "Desculpa, tive um problema para responder agora. Tenta de novo daqui a pouco. 🙏";

    public async Task<MessageProcessingResult> ProcessAsync(
        IncomingMessage message,
        CancellationToken cancellationToken = default)
    {
        var user = userDirectory.FindByPhoneNumber(message.From);

        if (user is null)
        {
            return MessageProcessingResult.UnknownSender;
        }

        if (IsTooOld(message))
        {
            return MessageProcessingResult.TooOld;
        }

        var isNewMessage = await conversationStore.TryAddIncomingMessageAsync(
            message.From,
            message.MessageId,
            message.Text,
            message.Timestamp,
            cancellationToken);

        if (!isNewMessage)
        {
            return MessageProcessingResult.Duplicate;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime);
        if (await briefDelivery.TryDeliverAsync(message.From, today, cancellationToken))
            return MessageProcessingResult.BriefDelivered;

        var conversation = await conversationStore.GetRecentTurnsAsync(message.From, HistoryLimit, cancellationToken);
        var userContext = await userContextProvider.GetContextAsync(user, cancellationToken);
        var request = new AssistantRequest(user.Name, conversation, userContext);

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

        return MessageProcessingResult.Replied;
    }

    private bool IsTooOld(IncomingMessage message) =>
        timeProvider.GetUtcNow() - message.Timestamp > MaxMessageAge;
}
