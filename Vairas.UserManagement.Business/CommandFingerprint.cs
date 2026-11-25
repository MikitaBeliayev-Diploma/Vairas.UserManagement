using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Encodings.Web;
using FluentValidation;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public static class CommandFingerprint
{
    // Fixed schema and property order; no timestamps, transport headers, or secrets.
    // Preserve argument values: whitespace differences are different commands.
    public static string Compute(ProfileCommand c, ActorContext actor) =>
        Convert.ToHexStringLower(SHA256.HashData(Canonicalize(c, actor)));

    public static byte[] Canonicalize(ProfileCommand c, ActorContext actor) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1, identityId = actor.IdentityId, workload = actor.Workload, channel = actor.Channel.ToString(),
            kind = c.Kind.ToString(), targetUserId = c.TargetUserId,
            firstName = c.FirstName, lastName = c.LastName, emailAddress = c.EmailAddress,
            phoneNumber = c.PhoneNumber, expectedRevision = c.ExpectedRevision,
            schoolId = c.SchoolId, invitationId = c.InvitationId, confirmationId = c.ConfirmationId, reason = c.Reason
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
