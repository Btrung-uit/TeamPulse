using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeamPulse.Application.Integrations;
using TeamPulse.Domain.Integrations;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Integrations.GitHub;

internal sealed class GitHubWebhookWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<GitHubWebhookOptions> options,
    ILogger<GitHubWebhookWorker> logger) : BackgroundService
{
    private const int MaximumAttempts = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (processed)
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "GitHub webhook worker polling failed.");
            }

            await Task.Delay(PollInterval, timeProvider, stoppingToken);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var handler = scope.ServiceProvider.GetRequiredService<IGitHubWebhookEventHandler>();
        var now = timeProvider.GetUtcNow();
        var leaseExpiredAt = now - ProcessingLease;
        var delivery = await dbContext.GitHubWebhookDeliveries
            .Where(item =>
                item.Status == GitHubWebhookDeliveryStatus.Received ||
                (item.Status == GitHubWebhookDeliveryStatus.Processing &&
                    item.LastAttemptedAtUtc <= leaseExpiredAt))
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (delivery is null)
        {
            return false;
        }

        delivery.MarkProcessing(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await handler.HandleAsync(
                new GitHubWebhookEvent(
                    delivery.Id,
                    delivery.DeliveryId,
                    delivery.EventName,
                    delivery.Action,
                    delivery.RepositoryId,
                    delivery.RepositoryFullName,
                    delivery.Payload),
                cancellationToken);
            logger.LogInformation(
                "Processed GitHub delivery {DeliveryId} ({EventName}.{Action}) for {Repository}",
                delivery.DeliveryId,
                delivery.EventName,
                delivery.Action ?? "none",
                delivery.RepositoryFullName ?? "unknown");
            delivery.MarkProcessed(timeProvider.GetUtcNow());
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "GitHub delivery {DeliveryId} processing failed on attempt {AttemptCount}",
                delivery.DeliveryId,
                delivery.AttemptCount);
            var error = exception.Message.Length <= GitHubWebhookDelivery.ErrorMaximumLength
                ? exception.Message
                : exception.Message[..GitHubWebhookDelivery.ErrorMaximumLength];
            dbContext.ChangeTracker.Clear();
            delivery = await dbContext.GitHubWebhookDeliveries.SingleAsync(
                item => item.Id == delivery.Id,
                cancellationToken);
            delivery.MarkProcessingFailure(error, MaximumAttempts);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
