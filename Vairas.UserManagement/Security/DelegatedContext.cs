// Versioned ReadPreviewV1 policy. Copies are checked by scripts/sync-policy.py.
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Vairas.Delegation;

internal sealed record DelegatedContext(Guid Identity, Guid Session, Guid School, string[] Scopes);
