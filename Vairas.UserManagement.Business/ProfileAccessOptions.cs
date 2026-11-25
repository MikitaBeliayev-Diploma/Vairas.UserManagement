using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed class ProfileAccessOptions
{
    public Guid[] PlatformOperatorIdentityIds { get; set; } = [];
    public string[] BootstrapWorkloads { get; set; } = [];
}
