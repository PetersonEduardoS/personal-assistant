using PersonalAssistant.Core.Messaging;

namespace PersonalAssistant.Api.Workers;

/// <summary>
/// Reads messages from the queue in the background.
/// For now it only logs them; the Claude call and the reply come in the next steps.
/// </summary>
public sealed class IncomingMessageWorker(
    IIncomingMessageQueue messageQueue,
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
                    logger.LogInformation("Received message {MessageId}", message.MessageId);
                }
                catch (Exception exception)
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
}
