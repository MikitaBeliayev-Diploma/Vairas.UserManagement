using Microsoft.Extensions.Options;

namespace Vairas.UserManagement.Configuration.Options;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options) =>
        Uri.TryCreate(options.Authority, UriKind.Absolute, out _) && Uri.TryCreate(options.Issuer, UriKind.Absolute, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Authentication:Authority and Authentication:Issuer must be absolute URLs.");
}
