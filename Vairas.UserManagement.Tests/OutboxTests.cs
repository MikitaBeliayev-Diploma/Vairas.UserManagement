using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Workers;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class OutboxTests
{
    private static OutboxMessage Message => new()
    {
        EventId = Guid.NewGuid(), AggregateId = Guid.NewGuid(), AggregateVersion = 2, EventType = "UserDeactivated",
        Body = "{\"payload\":{\"userId\":\"test\"}}", CorrelationId = "test", LeaseId = Guid.NewGuid(), Attempts = 1,
        TraceParent = "00-11111111111111111111111111111111-1111111111111111-01"
    };

    [Test]
    public async Task PublisherUsesStableMessageIdAndContractHeaders()
    {
        var sender = new Mock<ServiceBusSender>();
        ServiceBusMessage? sent = null;
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => sent = message).Returns(Task.CompletedTask);
        var client = new Mock<ServiceBusClient>();
        client.Setup(x => x.CreateSender("test-topic")).Returns(sender.Object);
        var message = Message;
        await using var publisher = new ServiceBusEventPublisher(client.Object, Options.Create(new OutboxOptions { TopicName = "test-topic" }));
        await publisher.PublishAsync(message, default);
        Assert.That(sent!.MessageId, Is.EqualTo(message.EventId.ToString("D")));
        Assert.That(sent.CorrelationId, Is.EqualTo(message.CorrelationId));
        Assert.That(sent.Body.ToString(), Is.EqualTo(message.Body));
        Assert.That(sent.ApplicationProperties["eventType"], Is.EqualTo("UserDeactivated"));
        Assert.That(sent.ApplicationProperties["traceparent"], Is.EqualTo(message.TraceParent));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WorkerAcknowledgesOnlySuccessfulPublication(bool fail)
    {
        var message = Message;
        var store = new Mock<IOutboxStore>();
        store.Setup(x => x.ClaimAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(message);
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(x => x.PublishAsync(message, It.IsAny<CancellationToken>()))
            .Returns(fail ? Task.FromException(new InvalidOperationException("test-only")) : Task.CompletedTask);
        using var provider = new ServiceCollection().AddSingleton(store.Object).AddSingleton(publisher.Object).BuildServiceProvider();
        using var worker = new OutboxWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new OutboxOptions()),
            NullLogger<OutboxWorker>.Instance);
        Assert.That(await worker.DispatchOnceAsync(default), Is.True);
        store.Verify(x => x.CompleteAsync(message.EventId, message.LeaseId!.Value, It.IsAny<CancellationToken>()), fail ? Times.Never() : Times.Once());
        store.Verify(x => x.FailAsync(message.EventId, message.LeaseId!.Value, It.Is<TimeSpan>(t => t > TimeSpan.Zero),
            "PUBLISH_FAILED", It.IsAny<CancellationToken>()), fail ? Times.Once() : Times.Never());
    }
}
