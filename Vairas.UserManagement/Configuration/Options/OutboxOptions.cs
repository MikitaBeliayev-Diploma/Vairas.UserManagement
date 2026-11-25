namespace Vairas.UserManagement.Configuration.Options;

public sealed class OutboxOptions
{
    public bool Enabled { get; set; }
    public string? ConnectionString { get; set; }
    public string TopicName { get; set; } = "vairas.user.deactivated.v1";
    public int PollSeconds { get; set; } = 2;
}
