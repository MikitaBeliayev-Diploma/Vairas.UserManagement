using Microsoft.EntityFrameworkCore;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Persistance.DataAccess;

namespace Vairas.UserManagement.Persistance;

public sealed class OutboxStore(VairasDbContext db, TimeProvider clock) : IOutboxStore
{
    public async Task<OutboxBacklog> GetBacklogAsync(CancellationToken ct) =>
        await db.Outbox.Where(x => x.PublishedAt == null).GroupBy(_ => 1)
            .Select(group => new OutboxBacklog(group.LongCount(), group.Min(x => x.OccurredAt))).SingleOrDefaultAsync(ct)
        ?? new(0, null);

    public async Task<OutboxMessage?> ClaimAsync(TimeSpan lease, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var rows = await db.Outbox.FromSqlInterpolated($"""
            SELECT * FROM outbox_message WHERE published_at IS NULL AND next_attempt_at <= {now}
              AND (leased_until IS NULL OR leased_until <= {now})
            ORDER BY occurred_at, event_id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var row = rows.SingleOrDefault();
        if (row is null) return null;
        row.LeaseId = Guid.NewGuid();
        row.LeasedUntil = now + lease;
        row.Attempts++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.Entry(row).State = EntityState.Detached;
        return row;
    }
    public async Task CompleteAsync(Guid eventId, Guid leaseId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.Outbox.Where(x => x.EventId == eventId && x.LeaseId == leaseId && x.PublishedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PublishedAt, now).SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.LeasedUntil, (DateTimeOffset?)null).SetProperty(x => x.LastErrorCode, (string?)null), ct);
    }
    public async Task FailAsync(Guid eventId, Guid leaseId, TimeSpan delay, string errorCode, CancellationToken ct)
    {
        var retry = clock.GetUtcNow() + delay;
        await db.Outbox.Where(x => x.EventId == eventId && x.LeaseId == leaseId && x.PublishedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, retry).SetProperty(x => x.LastErrorCode, errorCode)
                .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeasedUntil, (DateTimeOffset?)null), ct);
    }
}
