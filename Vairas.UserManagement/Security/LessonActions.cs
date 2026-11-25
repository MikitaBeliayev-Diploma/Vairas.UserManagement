using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vairas.ConfirmedActions;

public static class LessonActions
{
    public const string Profile = "ConfirmedLessonActionsV1";
    public const string Claim = "lesson_action";
    public const string RecoveryScope = "mcp.lesson-actions.reconcile";
    public const string OwnerRecoveryScope = "scheduling.lesson-actions.reconcile";
    public const string AuthorizationScope = "chat.lesson-actions.authorize";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false, RespectRequiredConstructorParameters = true };
    public static readonly JsonElement InputSchema = JsonSerializer.SerializeToElement(new
    {
        type = "object", additionalProperties = false, required = new[] { "binding", "arguments", "sourceRevisions" },
        properties = new
        {
            binding = new { type = "object", additionalProperties = false,
                required = new[] { "schoolId", "conversationId", "previewId", "operationId", "identityId", "userId", "sessionId", "kind", "actionDigest", "commandDigest" },
                properties = new[] { "schoolId", "conversationId", "previewId", "operationId", "identityId", "userId", "sessionId", "kind", "actionDigest", "commandDigest" }.ToDictionary(n => n, _ => new { type = "string" }) },
            arguments = new { type = "object" }, sourceRevisions = new { type = "object" }
        }
    });
    public static readonly JsonElement OutputSchema = JsonSerializer.SerializeToElement(new
    {
        type = "object", additionalProperties = false, required = new[] { "operationId", "status", "lessonId", "version", "code", "httpStatus" },
        properties = new { operationId = new { type = "string" }, status = new { type = "string", @enum = new[] { "Committed", "Rejected", "Unknown" } }, lessonId = new { type = new[] { "string", "null" } }, version = new { type = new[] { "integer", "null" } }, code = new { type = new[] { "string", "null" } }, httpStatus = new { type = "integer" } }
    });
    public static string Tool(string kind) => kind switch { "Booking" => "create_lesson_booking", "Rescheduling" => "reschedule_lesson_booking", _ => throw new InvalidOperationException("Unknown lesson action.") };
    public static string Scope(string kind) => "mcp." + Tool(kind);
    public static bool IsExecution(ClaimsPrincipal principal) => principal.HasClaim("vairas_delegation", Profile);
    public static string Digest(string kind, JsonElement arguments, JsonElement sourceRevisions) => Hash(JsonSerializer.SerializeToElement(new { kind, arguments, sourceRevisions }, Json));
    public static string Hash(JsonElement json)
    {
        // Integer-only deterministic JSON. Both endpoints validate the same frozen wire values.
        var buffer = new StringBuilder();
        void Write(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                    if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw new InvalidOperationException("Duplicate argument.");
                    buffer.Append('{'); for (var i = 0; i < properties.Length; i++) { if (i > 0) buffer.Append(','); buffer.Append(JsonSerializer.Serialize(properties[i].Name)); buffer.Append(':'); Write(properties[i].Value); } buffer.Append('}'); break;
                case JsonValueKind.Array:
                    buffer.Append('['); var first = true; foreach (var item in value.EnumerateArray()) { if (!first) buffer.Append(','); first = false; Write(item); } buffer.Append(']'); break;
                case JsonValueKind.String: buffer.Append(JsonSerializer.Serialize(value.GetString())); break;
                case JsonValueKind.Number:
                    if (!value.TryGetInt64(out var number) || number is < -9007199254740991 or > 9007199254740991) throw new InvalidOperationException("Unsafe argument number.");
                    buffer.Append(number.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
                case JsonValueKind.True: buffer.Append("true"); break;
                case JsonValueKind.False: buffer.Append("false"); break;
                case JsonValueKind.Null: buffer.Append("null"); break;
                default: throw new InvalidOperationException("Invalid argument.");
            }
        }
        Write(json); return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(buffer.ToString())));
    }
    public static void Validate(LessonAction action)
    {
        var b = action.Binding ?? throw new InvalidOperationException("Missing action binding.");
        if (new[] { b.SchoolId, b.ConversationId, b.PreviewId, b.OperationId, b.IdentityId, b.UserId, b.SessionId }.Contains(Guid.Empty)
            || b.ActionDigest is not { Length: 64 } || b.ActionDigest.Any(c => !char.IsAsciiHexDigitLower(c))
            || b.CommandDigest != Digest(b.Kind, action.Arguments, action.SourceRevisions)
            || action.Arguments.ValueKind != JsonValueKind.Object || action.SourceRevisions.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Invalid lesson action binding.");
        _ = Tool(b.Kind);
    }
    public static LessonActionBinding Parse(ClaimsPrincipal user, int depth)
    {
        string One(string name) => user.FindAll(name).ToArray() is { Length: 1 } c ? c[0].Value : throw new InvalidOperationException("Invalid action claim.");
        if (One("vairas_delegation") != Profile) throw new InvalidOperationException("Invalid execution profile.");
        var binding = JsonSerializer.Deserialize<LessonActionBinding>(One(Claim), Json) ?? throw new InvalidOperationException("Missing action binding.");
        using var actor = JsonDocument.Parse(One("act"));
        static bool Exact(JsonElement node, string name, bool nested) => node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Select(p => p.Name).Order().SequenceEqual((nested ? new[] { "act", "sub" } : new[] { "sub" }).Order()) && node.GetProperty("sub").GetString() == name;
        if (depth == 1 ? !Exact(actor.RootElement, "chat-backend", false) : depth != 2 || !Exact(actor.RootElement, "mcp-server", true) || !Exact(actor.RootElement.GetProperty("act"), "chat-backend", false)) throw new InvalidOperationException("Invalid execution actor.");
        if (new[] { binding.SchoolId, binding.ConversationId, binding.PreviewId, binding.OperationId, binding.IdentityId, binding.UserId, binding.SessionId }.Contains(Guid.Empty)
            || One("sub") != binding.IdentityId.ToString("D") || One("sid") != binding.SessionId.ToString("D") || One("school_id") != binding.SchoolId.ToString("D")
            || One("scope") != Scope(binding.Kind) || !long.TryParse(One("exp"), out var exp) || exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            || new[] { binding.ActionDigest, binding.CommandDigest }.Any(d => d is not { Length: 64 } || d.Any(c => !char.IsAsciiHexDigitLower(c)))) throw new InvalidOperationException("Invalid execution identity.");
        return binding;
    }
}
