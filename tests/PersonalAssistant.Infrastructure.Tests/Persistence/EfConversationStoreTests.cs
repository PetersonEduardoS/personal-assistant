using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class EfConversationStoreTests : IDisposable
{
    private const string Eduardo = "353800000001";
    private const string Karina = "353800000002";

    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task TryAddIncomingMessageAsync_ReturnsTrue_AndStoresMessage()
    {
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, TimeProvider.System);

        var added = await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Oi", DateTimeOffset.UnixEpoch);

        Assert.True(added);
        var stored = await context.ConversationMessages.SingleAsync();
        Assert.Equal(Eduardo, stored.PhoneNumber);
        Assert.Equal(ConversationRole.User, stored.Role);
        Assert.Equal("Oi", stored.Content);
        Assert.Equal("wamid.TEST1", stored.WhatsAppMessageId);
    }

    [Fact]
    public async Task TryAddIncomingMessageAsync_ReturnsFalse_WhenMessageWasAlreadyStored()
    {
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, TimeProvider.System);
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Oi", DateTimeOffset.UnixEpoch);

        var addedAgain = await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Oi", DateTimeOffset.UnixEpoch);

        Assert.False(addedAgain);
        Assert.Equal(1, await context.ConversationMessages.CountAsync());
    }

    [Fact]
    public async Task GetRecentTurnsAsync_ReturnsTurnsInChronologicalOrder()
    {
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, TimeProvider.System);
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Oi", DateTimeOffset.UnixEpoch);
        await store.AddAssistantReplyAsync(Eduardo, "Olá!");
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST2", "Tudo bem?", DateTimeOffset.UnixEpoch);

        var turns = await store.GetRecentTurnsAsync(Eduardo, limit: 20);

        Assert.Equal(
            new[]
            {
                new ConversationTurn(ConversationRole.User, "Oi"),
                new ConversationTurn(ConversationRole.Assistant, "Olá!"),
                new ConversationTurn(ConversationRole.User, "Tudo bem?")
            },
            turns);
    }

    [Fact]
    public async Task GetRecentTurnsAsync_ReturnsOnlyTheLatestTurns_UpToLimit()
    {
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, TimeProvider.System);
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Primeira", DateTimeOffset.UnixEpoch);
        await store.AddAssistantReplyAsync(Eduardo, "Resposta 1");
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST2", "Segunda", DateTimeOffset.UnixEpoch);
        await store.AddAssistantReplyAsync(Eduardo, "Resposta 2");
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST3", "Terceira", DateTimeOffset.UnixEpoch);

        var turns = await store.GetRecentTurnsAsync(Eduardo, limit: 3);

        Assert.Equal(new[] { "Segunda", "Resposta 2", "Terceira" }, turns.Select(turn => turn.Content));
    }

    [Fact]
    public async Task GetRecentTurnsAsync_SkipsLeadingAssistantTurns()
    {
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, TimeProvider.System);
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Primeira", DateTimeOffset.UnixEpoch);
        await store.AddAssistantReplyAsync(Eduardo, "Resposta 1");
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST2", "Segunda", DateTimeOffset.UnixEpoch);
        await store.AddAssistantReplyAsync(Eduardo, "Resposta 2");

        var turns = await store.GetRecentTurnsAsync(Eduardo, limit: 3);

        Assert.Equal(new[] { "Segunda", "Resposta 2" }, turns.Select(turn => turn.Content));
    }

    [Fact]
    public async Task GetRecentTurnsAsync_ReturnsOnlyTurnsFromThatPhoneNumber()
    {
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, TimeProvider.System);
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Oi, sou o Eduardo", DateTimeOffset.UnixEpoch);
        await store.TryAddIncomingMessageAsync(Karina, "wamid.TEST2", "Oi, sou a Karina", DateTimeOffset.UnixEpoch);

        var turns = await store.GetRecentTurnsAsync(Karina, limit: 20);

        var turn = Assert.Single(turns);
        Assert.Equal("Oi, sou a Karina", turn.Content);
    }

    [Fact]
    public async Task GetLastUserMessageAtAsync_ReturnsNull_WhenUserNeverSentMessage()
    {
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, TimeProvider.System);

        var lastSentAt = await store.GetLastUserMessageAtAsync(Eduardo);

        Assert.Null(lastSentAt);
    }

    [Fact]
    public async Task GetLastUserMessageAtAsync_ReturnsTimeOfLatestUserMessage_IgnoringRepliesAndOtherUsers()
    {
        var firstSentAt = new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
        var lastSentAt = new DateTimeOffset(2026, 11, 4, 21, 30, 0, TimeSpan.Zero);
        await using var context = _database.CreateContext();
        var store = new EfConversationStore(context, new FixedTimeProvider(lastSentAt.AddMinutes(5)));
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST1", "Bom dia", firstSentAt);
        await store.TryAddIncomingMessageAsync(Eduardo, "wamid.TEST2", "Boa noite", lastSentAt);
        await store.AddAssistantReplyAsync(Eduardo, "Boa noite!");
        await store.TryAddIncomingMessageAsync(Karina, "wamid.TEST3", "Oi", lastSentAt.AddHours(1));

        var result = await store.GetLastUserMessageAtAsync(Eduardo);

        Assert.Equal(lastSentAt, result);
    }
}
