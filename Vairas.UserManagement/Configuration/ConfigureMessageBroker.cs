using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;
using Vairas.UserManagement.Workers;

namespace Vairas.UserManagement.Configuration;

public static class ConfigureMessageBroker
{
    public static IServiceCollection AddMessageBroker(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<OutboxOptions>>().Value;
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new InvalidOperationException("Service Bus configuration is required.");
            return new ServiceBusClient(settings.ConnectionString, new ServiceBusClientOptions
            { RetryOptions = new ServiceBusRetryOptions { MaxRetries = 2, TryTimeout = TimeSpan.FromSeconds(10) } });
        });
        services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
        services.AddHostedService<OutboxWorker>();
        return services;
    }
}
