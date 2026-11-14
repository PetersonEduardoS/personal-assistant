using PersonalAssistant.Core.Appointments;

namespace PersonalAssistant.Api.Workers;

/// <summary>Checks for due appointment reminders every minute, including once at startup.</summary>
public sealed class AppointmentReminderWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<AppointmentReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var reminders = scope.ServiceProvider.GetRequiredService<AppointmentReminderService>();
                    var sent = await reminders.RunAsync(stoppingToken);
                    if (sent > 0)
                        logger.LogInformation("Sent {Count} appointment reminder(s)", sent);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    logger.LogError(exception,
                        "Appointment reminders failed. A claimed reminder is not sent again automatically");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}
