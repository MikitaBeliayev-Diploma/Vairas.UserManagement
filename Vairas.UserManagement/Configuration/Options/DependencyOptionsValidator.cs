using Microsoft.Extensions.Options;

namespace Vairas.UserManagement.Configuration.Options;

public sealed class DependencyOptionsValidator : IValidateOptions<DependencyOptions>
{
    public ValidateOptionsResult Validate(string? name, DependencyOptions options)
    {
        foreach (var origin in new[] { options.AuthBaseUrl, options.SchoolBaseUrl, options.ConfirmationBaseUrl, options.TokenEndpoint })
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
                return ValidateOptionsResult.Fail("Dependency origins must be absolute URLs without embedded credentials.");
        }
        return ValidateOptionsResult.Success;
    }
}
