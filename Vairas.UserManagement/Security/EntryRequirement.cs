using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Security;

public sealed record EntryRequirement(string? Scope = null) : IAuthorizationRequirement;
