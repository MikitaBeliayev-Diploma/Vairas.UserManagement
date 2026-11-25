using System.Diagnostics;
using System.Diagnostics.Metrics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Workers;

public sealed class OutboxWorker(IServiceScopeFactory scopes, IOptions<OutboxOptions> options,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    public static readonly ActivitySource Activities = new("Vairas.UserManagement");
    private static readonly Meter Meter = new("Vairas.UserManagement");
    private static readonly Counter<long> Published = Meter.CreateCounter<long>("outbox.published");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("outbox.failures");
    private static long pendingCount;
    private static double oldestAge;
    private static readonly ObservableGauge<long> Pending = Meter.CreateObservableGauge("outbox.pending", () => Volatile.Read(ref pendingCount));
    private static readonly ObservableGauge<double> Oldest = Meter.CreateObservableGauge("outbox.oldest.seconds", () => Volatile.Read(ref oldestAge));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var observedAt = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow - observedAt >= TimeSpan.FromSeconds(10))
                {
                    using var scope = scopes.CreateScope();
                    var state = await scope.ServiceProvider.GetRequiredService<IOutboxStore>().GetBacklogAsync(stoppingToken);
                    Volatile.Write(ref pendingCount, state.Pending);
                    Volatile.Write(ref oldestAge, state.OldestAt.HasValue ? Math.Max(0, (DateTimeOffset.UtcNow - state.OldestAt.Value).TotalSeconds) : 0);
                    observedAt = DateTimeOffset.UtcNow;
                }
                if (await DispatchOnceAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Outbox polling failed: {ExceptionType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task<bool> DispatchOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var message = await store.ClaimAsync(TimeSpan.FromMinutes(2), ct);
        if (message is null) return false;
        using var activity = Activities.StartActivity("outbox.publish", ActivityKind.Producer, message.TraceParent);
        activity?.SetTag("messaging.message.id", message.EventId.ToString());
        try
        {
            await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(message, ct);
            await store.CompleteAsync(message.EventId, message.LeaseId!.Value, ct);
            Published.Add(1);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Failures.Add(1);
            logger.LogWarning("Outbox event {EventId} failed: {ExceptionType}", message.EventId, ex.GetType().Name);
            var seconds = Math.Min(300, Math.Pow(2, Math.Min(message.Attempts, 8))) + Random.Shared.NextDouble();
            await store.FailAsync(message.EventId, message.LeaseId!.Value, TimeSpan.FromSeconds(seconds), "PUBLISH_FAILED", ct);
        }
        return true;
    }
}
