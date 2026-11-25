namespace Vairas.UserManagement.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid EventId { get; init; }
    public Guid AggregateId { get; init; }
    public long AggregateVersion { get; init; }
    public required string EventType { get; init; }
    public required string Body { get; init; }
    public required string CorrelationId { get; init; }
    public string? TraceParent { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeasedUntil { get; set; }
    public int Attempts { get; set; }
    public string? LastErrorCode { get; set; }
}
