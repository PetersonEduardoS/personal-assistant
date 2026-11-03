using PersonalAssistant.Core.Messaging;

namespace PersonalAssistant.Api.Workers;

/// <summary>
/// Reads messages from the queue in the background and hands each one to the processor.
/// A failure in one message is logged and does not stop the worker.
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

                    await processor.ProcessAsync(message, stoppingToken);

                    logger.LogInformation("Replied to message {MessageId}", message.MessageId);
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
}
