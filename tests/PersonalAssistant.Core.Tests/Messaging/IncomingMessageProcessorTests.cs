using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Media;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.Messaging;

public class IncomingMessageProcessorTests
{
    private const string Eduardo = "353800000001";

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static readonly IncomingMessage Message =
        new("wamid.TEST1", Eduardo, "Oi", Now.AddSeconds(-5));

    private static readonly UserDirectory Users =
        new([new UserProfile { PhoneNumber = Eduardo, Name = "Eduardo" }]);

    private readonly FakeConversationStore _store = new();
    private readonly FakeMessageSender _sender = new();
    private readonly FakeMorningBriefStore _briefs = new();
    private readonly FakeMediaDownloader _media = new();

    private static IncomingMessage Photo(string mimeType = "image/jpeg") => Message with
    {
        Text = "Salva essa consulta",
        Kind = IncomingMessageKind.Image,
        Attachment = new MessageAttachment("300", mimeType, null)
    };

    private static IncomingMessage Document(string mimeType, string fileName = "CA2 brief.pdf") => Message with
    {
        Text = "Resume pf",
        Kind = IncomingMessageKind.Document,
        Attachment = new MessageAttachment("400", mimeType, fileName)
    };

    [Fact]
    public async Task ProcessAsync_SendsThePhoto_ToTheAssistant_WithTheCaption()
    {
        var assistant = new FakeAssistantClient("Salvei a consulta!");

        var result = await CreateProcessor(assistant).ProcessAsync(Photo());

        Assert.Equal(MessageProcessingResult.Replied, result);
        Assert.Equal(("300", MediaLimits.MaxImageBytes), Assert.Single(_media.Downloads));
        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Equal(new byte[] { 1, 2, 3 }, request.Attachment!.Content);
        Assert.Equal("image/jpeg", request.Attachment.MimeType);
        Assert.Equal("[Foto] Salva essa consulta", request.Conversation.Last().Content);
        Assert.Equal((Eduardo, "Salvei a consulta!"), Assert.Single(_sender.SentMessages));
    }

    [Fact]
    public async Task ProcessAsync_SendsAPdf_ToTheAssistant()
    {
        var assistant = new FakeAssistantClient("Resumo: ...");

        await CreateProcessor(assistant).ProcessAsync(Document("application/pdf"));

        Assert.Equal(("400", MediaLimits.MaxDocumentBytes), Assert.Single(_media.Downloads));
        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Equal("application/pdf", request.Attachment!.MimeType);
        Assert.Equal("[Arquivo: CA2 brief.pdf] Resume pf", request.Conversation.Last().Content);
    }

    [Theory]
    [InlineData(IncomingMessageKind.Document, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(IncomingMessageKind.Image, "image/heic")]
    public async Task ProcessAsync_SaysWhichFilesItReads_ForOtherFileTypes(IncomingMessageKind kind, string mimeType)
    {
        var assistant = new FakeAssistantClient("Olá!");
        var message = kind == IncomingMessageKind.Image ? Photo(mimeType) : Document(mimeType, "notas.docx");

        var result = await CreateProcessor(assistant).ProcessAsync(message);

        Assert.Equal(MessageProcessingResult.Unsupported, result);
        Assert.Empty(_media.Downloads);
        Assert.Empty(assistant.ReceivedRequests);
        Assert.Equal(IncomingMessageProcessor.UnsupportedFileReply, Assert.Single(_sender.SentMessages).Text);
    }

    [Theory]
    [InlineData(true, "5 MB")]
    [InlineData(false, "10 MB")]
    public async Task ProcessAsync_SaysTheLimit_WhenTheFileIsTooLarge(bool isPhoto, string limit)
    {
        _media.Failure = new MediaTooLargeException(1);
        var assistant = new FakeAssistantClient("Olá!");

        var result = await CreateProcessor(assistant).ProcessAsync(isPhoto ? Photo() : Document("application/pdf"));

        Assert.Equal(MessageProcessingResult.Unsupported, result);
        Assert.Empty(assistant.ReceivedRequests);
        Assert.Contains(limit, Assert.Single(_sender.SentMessages).Text);
    }

    [Fact]
    public async Task ProcessAsync_AsksToSendAgain_WhenTheDownloadFails()
    {
        _media.Failure = new MediaUnavailableException("expired");
        var assistant = new FakeAssistantClient("Olá!");

        await CreateProcessor(assistant).ProcessAsync(Photo());

        Assert.Empty(assistant.ReceivedRequests);
        Assert.Equal(IncomingMessageProcessor.DownloadFailedReply, Assert.Single(_sender.SentMessages).Text);
    }

    [Fact]
    public async Task ProcessAsync_SendsAssistantReply_ToSender()
    {
        var processor = CreateProcessor(new FakeAssistantClient("Olá!"));

        var result = await processor.ProcessAsync(Message);

        Assert.Equal(MessageProcessingResult.Replied, result);
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
    public async Task ProcessAsync_SendsUserContext_ToAssistant()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var processor = CreateProcessor(assistant, userContext: "Desenvolvedor .NET em Dublin.");

        await processor.ProcessAsync(Message);

        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Equal("Desenvolvedor .NET em Dublin.", request.UserContext);
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
        var result = await processor.ProcessAsync(Message);

        Assert.Equal(MessageProcessingResult.Duplicate, result);
        Assert.Single(assistant.ReceivedRequests);
        Assert.Single(_sender.SentMessages);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresMessage_WhenSenderIsUnknown()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var processor = CreateProcessor(assistant);

        var result = await processor.ProcessAsync(Message with { From = "353899999999" });

        Assert.Equal(MessageProcessingResult.UnknownSender, result);
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

    [Fact]
    public async Task ProcessAsync_IgnoresMessage_WhenOlderThanMaxAge()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var processor = CreateProcessor(assistant);
        var oldMessage = Message with
        {
            Timestamp = Now - IncomingMessageProcessor.MaxMessageAge - TimeSpan.FromMinutes(1)
        };

        var result = await processor.ProcessAsync(oldMessage);

        Assert.Equal(MessageProcessingResult.TooOld, result);
        Assert.Empty(assistant.ReceivedRequests);
        Assert.Empty(_sender.SentMessages);
        Assert.Empty(_store.Turns);
    }

    [Fact]
    public async Task ProcessAsync_RepliesToMessage_WhenSentJustUnderMaxAge()
    {
        var processor = CreateProcessor(new FakeAssistantClient("Olá!"));
        var recentMessage = Message with
        {
            Timestamp = Now - IncomingMessageProcessor.MaxMessageAge + TimeSpan.FromMinutes(1)
        };

        var result = await processor.ProcessAsync(recentMessage);

        Assert.Equal(MessageProcessingResult.Replied, result);
        Assert.Single(_sender.SentMessages);
    }

    [Fact]
    public async Task ProcessAsync_DeliversTodaysBriefAndNextMessageCanDiscussIt()
    {
        var date = DateOnly.FromDateTime(Now.DateTime);
        await _briefs.TryAddAsync(Eduardo, date, "Bom dia! Treino às 11h.");
        var assistant = new FakeAssistantClient("Pode treinar depois.");
        var processor = CreateProcessor(assistant);

        Assert.Equal(MessageProcessingResult.BriefDelivered, await processor.ProcessAsync(Message));
        Assert.Empty(assistant.ReceivedRequests);
        Assert.Equal("Bom dia! Treino às 11h.", Assert.Single(_sender.SentMessages).Text);
        Assert.True((await _briefs.GetAsync(Eduardo, date))!.IsDelivered);
        Assert.Equal(MessageProcessingResult.Duplicate, await processor.ProcessAsync(Message));

        await processor.ProcessAsync(Message with { MessageId = "wamid.NEXT", Text = "E se eu treinar mais tarde?" });
        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Contains(request.Conversation, turn => turn.Role == ConversationRole.Assistant && turn.Content == "Bom dia! Treino às 11h.");
        Assert.Equal(2, _sender.SentMessages.Count);
    }

    [Theory]
    [InlineData("audio", "Ainda não entendo áudio, pode mandar por texto? 😊", "[Áudio]")]
    [InlineData("video", "Ainda não entendo vídeo, pode mandar por texto? 😊", "[Vídeo]")]
    [InlineData("sticker", "Ainda não entendo figurinha, pode mandar por texto? 😊", "[Figurinha]")]
    [InlineData("location", "Ainda não entendo localização, pode mandar por texto? 😊", "[Localização]")]
    public async Task ProcessAsync_SaysWhatItCannotUnderstandYet_WithoutAskingTheAssistant(
        string type, string expectedReply, string expectedHistory)
    {
        var assistant = new FakeAssistantClient("Olá!");
        var message = Message with { Text = string.Empty, Kind = IncomingMessageKind.Unsupported, UnsupportedType = type };

        var result = await CreateProcessor(assistant).ProcessAsync(message);

        Assert.Equal(MessageProcessingResult.Unsupported, result);
        Assert.Empty(assistant.ReceivedRequests);
        Assert.Equal((Eduardo, expectedReply), Assert.Single(_sender.SentMessages));
        Assert.Equal(
            new[]
            {
                new ConversationTurn(ConversationRole.User, expectedHistory),
                new ConversationTurn(ConversationRole.Assistant, expectedReply)
            },
            _store.Turns.Select(turn => turn.Turn).ToArray());
    }

    [Fact]
    public async Task ProcessAsync_StillDeliversThePendingBrief_WhenTheReplyIsAnAudio()
    {
        var date = DateOnly.FromDateTime(Now.DateTime);
        await _briefs.TryAddAsync(Eduardo, date, "Bom dia, mooooor ☀️");
        var message = Message with { Text = string.Empty, Kind = IncomingMessageKind.Unsupported, UnsupportedType = "audio" };

        var result = await CreateProcessor(new FakeAssistantClient("Olá!")).ProcessAsync(message);

        Assert.Equal(MessageProcessingResult.BriefDelivered, result);
        Assert.Equal("Bom dia, mooooor ☀️", Assert.Single(_sender.SentMessages).Text);
    }

    [Fact]
    public async Task ProcessAsync_KeepsTheCaption_InTheHistoryOfAPhoto()
    {
        var message = Message with
        {
            Text = "Salva essa consulta",
            Kind = IncomingMessageKind.Image,
            Attachment = new MessageAttachment("300", "image/jpeg", null)
        };

        await CreateProcessor(new FakeAssistantClient("Olá!")).ProcessAsync(message);

        Assert.Equal("[Foto] Salva essa consulta", _store.Turns.First().Turn.Content);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotDeliverYesterdaysBrief()
    {
        await _briefs.TryAddAsync(Eduardo, DateOnly.FromDateTime(Now.DateTime).AddDays(-1), "Old brief");
        var assistant = new FakeAssistantClient("Olá!");
        Assert.Equal(MessageProcessingResult.Replied, await CreateProcessor(assistant).ProcessAsync(Message));
        Assert.Single(assistant.ReceivedRequests);
    }

    [Fact]
    public async Task ProcessAsync_GivesTheTools_ToTheAssistant()
    {
        var assistant = new FakeAssistantClient("Olá!");
        var processor = CreateProcessor(assistant);

        await processor.ProcessAsync(Message);

        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Equal(
            new[]
            {
                "add_appointment", "list_appointments", "cancel_appointment", "list_work_shifts", "list_deadlines",
                "save_note", "list_notes", "delete_note", "set_morning_brief_time"
            },
            request.Tools!.Select(tool => tool.Name));
    }

    private IncomingMessageProcessor CreateProcessor(IAssistantClient assistant, string? userContext = null)
    {
        var dublin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin");
        var clock = new FixedTimeProvider(Now);

        return new(assistant, _sender, Users, _store, new FakeUserContextProvider(userContext), clock,
            new MorningBriefDelivery(_briefs, _sender, _store, new MorningBriefDeliveryGate()),
            dublin,
            new AssistantToolFactory(new FakeAppointmentStore(), new FakeWorkScheduleProvider(), new FakeDeadlineProvider(),
                clock, dublin, new FakeNoteStore(), new FakeEmailInbox(), new FakeMorningBriefPreferenceStore(), new FakeShoppingListStore()),
            _media);
    }
}
