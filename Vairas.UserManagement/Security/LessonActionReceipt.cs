using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vairas.ConfirmedActions;

/// <summary>Owner receipt; Unknown never implies that a business change failed.</summary>
public sealed record LessonActionReceipt(Guid OperationId, string Status, Guid? LessonId = null, int? Version = null, string? Code = null, int HttpStatus = 202);
