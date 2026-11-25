namespace Vairas.UserManagement.Configuration.Options;

public sealed class DependencyOptions
{
    public string AuthBaseUrl { get; set; } = "https://localhost:7100/";
    public string SchoolBaseUrl { get; set; } = "https://localhost:7200/";
    public string ConfirmationBaseUrl { get; set; } = "https://localhost:7300/";
    public string TokenEndpoint { get; set; } = "https://localhost:7100/connect/token";
    public string ClientId { get; set; } = "user-management";
    public string? ClientSecret { get; set; }
    public string Scope { get; set; } = "identity.validate school.profile-provisioning confirmations.validate";
}
