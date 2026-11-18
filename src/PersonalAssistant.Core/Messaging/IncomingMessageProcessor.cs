using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Handles one received message: saves it, asks the assistant for a reply
/// using the recent conversation and the user's context, sends the reply and saves it too.
/// The assistant can use tools to manage the user's appointments and to read their work shifts and deadlines.
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
    TimeZoneInfo timeZone,
    AssistantToolFactory tools)
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
            DescribeForHistory(message),
            message.Timestamp,
            cancellationToken);

        if (!isNewMessage)
        {
            return MessageProcessingResult.Duplicate;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime);
        if (await briefDelivery.TryDeliverAsync(message.From, today, cancellationToken))
            return MessageProcessingResult.BriefDelivered;

        if (message.Kind != IncomingMessageKind.Text)
        {
            // Photos and documents are read in a later stage; until then they get the same honest reply.
            var unsupportedReply = UnsupportedReply(message);
            await messageSender.SendTextAsync(message.From, unsupportedReply, cancellationToken);
            await conversationStore.AddAssistantReplyAsync(message.From, unsupportedReply, cancellationToken);

            return MessageProcessingResult.Unsupported;
        }

        var conversation = await conversationStore.GetRecentTurnsAsync(message.From, HistoryLimit, cancellationToken);
        var userContext = await userContextProvider.GetContextAsync(user, cancellationToken);
        var request = new AssistantRequest(user.Name, conversation, userContext, tools.CreateFor(user));

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

    /// <summary>
    /// What goes in the conversation history: the text, or a label such as "[Foto] caption" or "[Áudio]",
    /// so the assistant knows later what was sent.
    /// </summary>
    internal static string DescribeForHistory(IncomingMessage message)
    {
        var label = message.Kind switch
        {
            IncomingMessageKind.Text => null,
            IncomingMessageKind.Image => "[Foto]",
            IncomingMessageKind.Document => $"[Arquivo: {message.Attachment?.FileName ?? "sem nome"}]",
            _ => $"[{Capitalize(TypeName(message.UnsupportedType))}]"
        };

        if (label is null)
        {
            return message.Text;
        }

        return string.IsNullOrWhiteSpace(message.Text) ? label : $"{label} {message.Text}";
    }

    internal static string UnsupportedReply(IncomingMessage message)
    {
        var name = message.Kind switch
        {
            IncomingMessageKind.Image => "foto",
            IncomingMessageKind.Document => "arquivo",
            _ => TypeName(message.UnsupportedType)
        };

        return $"Ainda não entendo {name}, pode mandar por texto? 😊";
    }

    private static string TypeName(string? whatsAppType) => whatsAppType switch
    {
        "audio" => "áudio",
        "video" => "vídeo",
        "sticker" => "figurinha",
        "location" => "localização",
        "contacts" => "contato",
        _ => "esse tipo de mensagem"
    };

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private bool IsTooOld(IncomingMessage message) =>
        timeProvider.GetUtcNow() - message.Timestamp > MaxMessageAge;
}
