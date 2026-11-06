using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Api.Workers;

/// <summary>Checks Dublin schedules every minute, including once at startup.</summary>
public sealed class MorningBriefWorker(
    IServiceScopeFactory scopeFactory,
    UserDirectory users,
    TimeProvider clock,
    ILogger<MorningBriefWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
            do
            {
                foreach (var user in users.All.Where(user => user.MorningBriefTime is not null ||
                    !string.IsNullOrWhiteSpace(user.MorningBriefRoutineFilePath)))
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var scheduler = scope.ServiceProvider.GetRequiredService<MorningBriefScheduler>();
                        var result = await scheduler.RunAsync(user, stoppingToken);
                        if (result is MorningBriefResult.TextSent or MorningBriefResult.TemplateSent)
                            logger.LogInformation("Morning brief for {UserName}: {Result}", user.Name, result);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError(exception,
                            "Morning brief failed for {UserName}. If already stored, it will not be sent again automatically; inspect the stored brief and Meta logs",
                            user.Name);
                    }
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}
