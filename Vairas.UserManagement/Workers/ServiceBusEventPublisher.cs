using System.Diagnostics;
using System.Diagnostics.Metrics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Workers;

public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient client;
    private readonly ServiceBusSender sender;
    public ServiceBusEventPublisher(ServiceBusClient serviceBusClient, IOptions<OutboxOptions> options)
    {
        var settings = options.Value;
        client = serviceBusClient;
        sender = client.CreateSender(settings.TopicName);
    }
    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        var outbound = new ServiceBusMessage(BinaryData.FromString(message.Body))
        {
            MessageId = message.EventId.ToString("D"), CorrelationId = message.CorrelationId,
            Subject = message.EventType, ContentType = "application/json"
        };
        outbound.ApplicationProperties["eventType"] = message.EventType;
        outbound.ApplicationProperties["eventVersion"] = 1;
        if (message.TraceParent is not null) outbound.ApplicationProperties["traceparent"] = message.TraceParent;
        await sender.SendMessageAsync(outbound, ct);
    }
    public async ValueTask DisposeAsync()
    {
        await sender.DisposeAsync();
        // The client is owned and disposed by the DI container.
    }
}
