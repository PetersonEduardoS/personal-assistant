using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Conversations;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class EfConversationStore(AssistantDbContext context, TimeProvider timeProvider) : IConversationStore
{
    private const int SqliteConstraintErrorCode = 19;

    public async Task<bool> TryAddIncomingMessageAsync(
        string phoneNumber,
        string whatsAppMessageId,
        string content,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        var alreadyStored = await context.ConversationMessages
            .AnyAsync(message => message.WhatsAppMessageId == whatsAppMessageId, cancellationToken);

        if (alreadyStored)
        {
            return false;
        }

        context.ConversationMessages.Add(new ConversationMessage
        {
            PhoneNumber = phoneNumber,
            Role = ConversationRole.User,
            Content = content,
            WhatsAppMessageId = whatsAppMessageId,
            CreatedAtUtc = receivedAt.UtcDateTime
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintErrorCode })
        {
            // Another delivery of the same webhook was saved at the same moment.
            context.ChangeTracker.Clear();

            return false;
        }
    }

    public async Task AddAssistantReplyAsync(
        string phoneNumber,
        string content,
        CancellationToken cancellationToken = default)
    {
        context.ConversationMessages.Add(new ConversationMessage
        {
            PhoneNumber = phoneNumber,
            Role = ConversationRole.Assistant,
            Content = content,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(
        string phoneNumber,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var latestTurns = await context.ConversationMessages
            .AsNoTracking()
            .Where(message => message.PhoneNumber == phoneNumber)
            .OrderByDescending(message => message.Id)
            .Take(limit)
            .Select(message => new ConversationTurn(message.Role, message.Content))
            .ToListAsync(cancellationToken);

        latestTurns.Reverse();

        // The Claude API expects the conversation to start with a user message.
        return latestTurns
            .SkipWhile(turn => turn.Role == ConversationRole.Assistant)
            .ToList();
    }
}
