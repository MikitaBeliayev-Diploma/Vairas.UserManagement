using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vairas.ConfirmedActions;

/// <summary>Immutable identity of one exact human-approved practical lesson command.</summary>
public sealed record LessonActionBinding(Guid SchoolId, Guid ConversationId, Guid PreviewId, Guid OperationId,
    Guid IdentityId, Guid UserId, Guid SessionId, string Kind, string ActionDigest, string CommandDigest);
