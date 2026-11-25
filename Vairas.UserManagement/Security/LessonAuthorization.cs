using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vairas.ConfirmedActions;

/// <summary>Permission is valid only for the exact supplied command and current execution intent.</summary>
public sealed record LessonAuthorization(bool Allowed, DateTimeOffset? ConfirmedAt = null, DateTimeOffset? ExpiresAt = null);
