using Microsoft.Extensions.Options;

namespace Vairas.UserManagement.Configuration.Options;

/// <summary>A disabled outbox needs no broker credentials; topic and polling interval are always checked.</summary>
public sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        var failures = new List<string>();
        if (options.Enabled && string.IsNullOrWhiteSpace(options.ConnectionString))
            failures.Add("Enabled outbox requires Service Bus credentials.");
        if (string.IsNullOrWhiteSpace(options.TopicName) || options.PollSeconds <= 0)
            failures.Add("Invalid outbox topic or polling interval.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
