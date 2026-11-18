using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Media;
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
    AssistantToolFactory tools,
    IMediaDownloader mediaDownloader)
{
    public const int HistoryLimit = 20;

    /// <summary>
    /// Meta retries webhooks that failed, sometimes hours later. A reply to a message
    /// older than this would arrive out of context, so the message is skipped.
    /// </summary>
    public static readonly TimeSpan MaxMessageAge = TimeSpan.FromMinutes(10);

    public const string UnsupportedFileReply =
        "Por enquanto eu só abro fotos (JPEG, PNG ou WebP) e PDFs. Esse arquivo eu ainda não consigo ler. 😊";

    public const string DownloadFailedReply = "Não consegui baixar o arquivo. Pode mandar de novo? 🙏";

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

        if (message.Kind == IncomingMessageKind.Unsupported)
        {
            return await ReplyWithoutAssistantAsync(message.From, UnsupportedReply(message), cancellationToken);
        }

        AssistantAttachment? attachment = null;

        if (message.Kind is IncomingMessageKind.Image or IncomingMessageKind.Document)
        {
            var (downloaded, problem) = await DownloadAsync(message, cancellationToken);

            if (downloaded is null)
            {
                return await ReplyWithoutAssistantAsync(message.From, problem!, cancellationToken);
            }

            attachment = downloaded;
        }

        var conversation = await conversationStore.GetRecentTurnsAsync(message.From, HistoryLimit, cancellationToken);
        var userContext = await userContextProvider.GetContextAsync(user, cancellationToken);
        var request = new AssistantRequest(user.Name, conversation, userContext, tools.CreateFor(user), attachment);

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

    internal static string UnsupportedReply(IncomingMessage message) =>
        $"Ainda não entendo {TypeName(message.UnsupportedType)}, pode mandar por texto? 😊";

    /// <summary>
    /// Downloads a photo or PDF the assistant can read. Returns the attachment, or the reply that explains
    /// why it cannot be read: another file type, too large, or a failed download. Nothing is retried,
    /// because the temporary download address expires in a few minutes.
    /// </summary>
    private async Task<(AssistantAttachment? Attachment, string? Problem)> DownloadAsync(
        IncomingMessage message,
        CancellationToken cancellationToken)
    {
        var mimeType = MediaLimits.Normalize(message.Attachment?.MimeType);
        var isPhoto = message.Kind == IncomingMessageKind.Image;
        var readable = isPhoto ? MediaLimits.ImageTypes.Contains(mimeType) : mimeType == MediaLimits.PdfType;

        if (message.Attachment is null || !readable)
        {
            return (null, UnsupportedFileReply);
        }

        var maxBytes = isPhoto ? MediaLimits.MaxImageBytes : MediaLimits.MaxDocumentBytes;

        try
        {
            var media = await mediaDownloader.DownloadAsync(message.Attachment.MediaId, maxBytes, cancellationToken);

            return (new AssistantAttachment(media.Content, mimeType), null);
        }
        catch (MediaTooLargeException)
        {
            var limit = $"{maxBytes / (1024 * 1024)} MB";

            return (null, isPhoto
                ? $"Essa foto é grande demais para mim (o limite é {limit}). 😅"
                : $"Esse PDF é grande demais para mim (o limite é {limit} e até 100 páginas). 😅");
        }
        catch (MediaUnavailableException)
        {
            return (null, DownloadFailedReply);
        }
    }

    private async Task<MessageProcessingResult> ReplyWithoutAssistantAsync(
        string recipient,
        string reply,
        CancellationToken cancellationToken)
    {
        await messageSender.SendTextAsync(recipient, reply, cancellationToken);
        await conversationStore.AddAssistantReplyAsync(recipient, reply, cancellationToken);

        return MessageProcessingResult.Unsupported;
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
