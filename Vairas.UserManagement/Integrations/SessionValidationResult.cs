using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Integrations;

public sealed record SessionValidationResult(bool Active, Guid IdentityId, Guid SessionId, DateTimeOffset ValidUntil);
