using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Infrastructure.Persistence;

namespace PersonalAssistant.Infrastructure.Tests.Persistence;

public sealed class AssistantDbContextTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Migrations_CreateConversationMessagesTable()
    {
        await using (var context = _database.CreateContext())
        {
            context.ConversationMessages.Add(NewMessage("wamid.TEST1", ConversationRole.User, "Oi"));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var message = await context.ConversationMessages.SingleAsync();

            Assert.Equal("353800000001", message.PhoneNumber);
            Assert.Equal(ConversationRole.User, message.Role);
            Assert.Equal("Oi", message.Content);
            Assert.Equal("wamid.TEST1", message.WhatsAppMessageId);
        }
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenWhatsAppMessageIdIsRepeated()
    {
        await using var context = _database.CreateContext();
        context.ConversationMessages.Add(NewMessage("wamid.TEST1", ConversationRole.User, "Oi"));
        await context.SaveChangesAsync();

        context.ConversationMessages.Add(NewMessage("wamid.TEST1", ConversationRole.User, "Oi de novo"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_AllowsSeveralMessages_WithoutWhatsAppMessageId()
    {
        await using var context = _database.CreateContext();
        context.ConversationMessages.Add(NewMessage(null, ConversationRole.Assistant, "Olá!"));
        context.ConversationMessages.Add(NewMessage(null, ConversationRole.Assistant, "Tudo bem?"));

        await context.SaveChangesAsync();

        Assert.Equal(2, await context.ConversationMessages.CountAsync());
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenUserHasTwoMorningBriefsOnSameDay()
    {
        await using var context = _database.CreateContext();
        context.MorningBriefs.Add(NewBrief("Primeiro resumo"));
        await context.SaveChangesAsync();

        context.MorningBriefs.Add(NewBrief("Segundo resumo"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static MorningBriefEntry NewBrief(string content) =>
        new()
        {
            PhoneNumber = "353800000001",
            Date = new DateOnly(2026, 11, 5),
            Content = content,
            CreatedAtUtc = DateTime.UtcNow
        };

    private static ConversationMessage NewMessage(string? whatsAppMessageId, ConversationRole role, string content) =>
        new()
        {
            PhoneNumber = "353800000001",
            Role = role,
            Content = content,
            WhatsAppMessageId = whatsAppMessageId,
            CreatedAtUtc = DateTime.UtcNow
        };
}
