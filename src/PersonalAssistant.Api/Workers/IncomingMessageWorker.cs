using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Api.Workers;

/// <summary>
/// Reads messages from the queue in the background and hands each one to the processor.
/// The outcome of every message is logged, and a failure in one message does not stop the worker.
/// </summary>
public sealed class IncomingMessageWorker(
    IIncomingMessageQueue messageQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<IncomingMessageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in messageQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IncomingMessageProcessor>();

                    var result = await processor.ProcessAsync(message, stoppingToken);

                    LogResult(message, result);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Failed to process message {MessageId}", message.MessageId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The application is shutting down.
        }
    }

    private void LogResult(IncomingMessage message, MessageProcessingResult result)
    {
        switch (result)
        {
            case MessageProcessingResult.BriefDelivered:
                logger.LogInformation("Delivered stored morning brief in response to message {MessageId}", message.MessageId);
                break;
            case MessageProcessingResult.Replied:
                logger.LogInformation("Replied to message {MessageId}", message.MessageId);
                break;

            case MessageProcessingResult.Duplicate:
                logger.LogInformation("Ignored duplicate message {MessageId}", message.MessageId);
                break;

            case MessageProcessingResult.UnknownSender:
                logger.LogWarning(
                    "Ignored message {MessageId} from a number that is not in the user list",
                    message.MessageId);
                break;

            case MessageProcessingResult.TooOld:
                logger.LogWarning(
                    "Ignored message {MessageId} sent at {SentAt:u}, older than {MaxMessageAge}",
                    message.MessageId,
                    message.Timestamp,
                    IncomingMessageProcessor.MaxMessageAge);
                break;
        }
    }
}
