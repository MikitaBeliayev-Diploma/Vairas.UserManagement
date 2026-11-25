// Versioned ReadPreviewV1 policy. Copies are checked by scripts/sync-policy.py.
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Vairas.Delegation;
internal static class ReadPreviewDelegation
{
    internal const string Profile = "ReadPreviewV1";
    internal const string Grant = "urn:ietf:params:oauth:grant-type:token-exchange";
    internal const string AccessTokenType = "urn:ietf:params:oauth:token-type:access_token";
    internal const string ContextScope = "mcp.context.read";
    internal static readonly string[] Tools = ["get_my_enrollments", "get_my_lessons", "get_student_training_progress", "find_available_lessons", "preview_lesson_booking", "preview_lesson_rescheduling"];
    internal static string[] Scopes => [ContextScope, .. Tools.Select(t => "mcp." + t)];
    internal static bool Marked(ClaimsPrincipal user) => user.HasClaim(c => c.Type is "act" or "school_id" or "vairas_delegation");
    internal static DelegatedContext Parse(ClaimsPrincipal user, int depth)
    {
        string One(string name) => user.FindAll(name).ToArray() is { Length: 1 } c ? c[0].Value : throw new InvalidOperationException("Invalid delegation binding.");
        Guid Id(string name) => Guid.TryParse(One(name), out var id) && id != Guid.Empty ? id : throw new InvalidOperationException("Invalid delegation identity.");
        if (One("vairas_delegation") != Profile) throw new InvalidOperationException("Unknown delegation profile.");
        using var actor = JsonDocument.Parse(One("act"));
        static bool Exact(JsonElement node, string name, bool nested) => node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Select(p => p.Name).Order().SequenceEqual((nested ? new[] { "act", "sub" } : new[] { "sub" }).Order()) && node.GetProperty("sub").GetString() == name;
        if (depth == 1 ? !Exact(actor.RootElement, "chat-backend", false) : depth != 2 || !Exact(actor.RootElement, "mcp-server", true) || !Exact(actor.RootElement.GetProperty("act"), "chat-backend", false)) throw new InvalidOperationException("Invalid delegation chain.");
        var scopes = One("scope").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (scopes.Length == 0 || scopes.Distinct(StringComparer.Ordinal).Count() != scopes.Length || scopes.Except(Scopes, StringComparer.Ordinal).Any()) throw new InvalidOperationException("Invalid delegation scope.");
        if (!long.TryParse(One("exp"), out var expiry) || expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new InvalidOperationException("Expired delegation.");
        return new(Id("sub"), Id("sid"), Id("school_id"), scopes);
    }
    internal static string? RouteScope(string owner, string method, string path)
    {
        bool Is(string pattern) => Regex.IsMatch(path, "^" + pattern + "/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        const string id = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
        if (method == "GET")
        {
            if (owner == "user-management" && Is("/api/v1/users/me")) return ContextScope;
            if (owner == "school-management" && Is("/api/v1/users/me/enrollments")) return "mcp.get_my_enrollments";
            if (owner == "scheduling" && Is("/api/v1/users/me/lessons")) return "mcp.get_my_lessons";
            if (owner == "scheduling" && Is("/api/v1/schools/" + id + "/availability")) return "mcp.find_available_lessons";
            if (owner == "learning-tracking" && Is("/api/v1/student-trainings/" + id + "/progress")) return "mcp.get_student_training_progress";
        }
        if (method == "POST" && owner == "scheduling")
        {
            if (Is("/api/v1/practical-lessons/booking-previews")) return "mcp.preview_lesson_booking";
            if (Is("/api/v1/practical-lessons/" + id + "/rescheduling-previews")) return "mcp.preview_lesson_rescheduling";
        }
        return null;
    }
    internal static string[] ResourceScopes(string owner) => owner switch
    {
        "user-management" => [ContextScope], "school-management" => ["mcp.get_my_enrollments"],
        "scheduling" => ["mcp.get_my_lessons", "mcp.find_available_lessons", "mcp.preview_lesson_booking", "mcp.preview_lesson_rescheduling"],
        "learning-tracking" => ["mcp.get_student_training_progress"], _ => []
    };
    internal static async Task<bool> AllowedAsync(HttpContext http, string owner, bool checkBody)
    {
        if (!Marked(http.User)) return true;
        try
        {
            if (http.User.Identity?.IsAuthenticated != true) return false;
            if (Vairas.ConfirmedActions.LessonActions.IsExecution(http.User))
            {
                var binding = Vairas.ConfirmedActions.LessonActions.Parse(http.User, 2);
                var expected = binding.Kind == "Booking" ? "/api/v1/practical-lessons/confirmed-bookings" : "/api/v1/practical-lessons/confirmed-reschedulings";
                if (owner != "scheduling" || http.Request.Method != "POST" || http.Request.Path != expected || http.User.FindAll("aud").ToArray() is not { Length: 1 } executionAudiences || executionAudiences[0].Value != "vairas-scheduling") return false;
                if (!checkBody) return true;
                http.Request.EnableBuffering(65536, 65536);
                try
                {
                    var action = await JsonSerializer.DeserializeAsync<Vairas.ConfirmedActions.LessonAction>(http.Request.Body, Vairas.ConfirmedActions.LessonActions.Json, http.RequestAborted);
                    if (action is null || action.Binding != binding || http.Request.Headers["Idempotency-Key"].ToString() != binding.OperationId.ToString("D")) return false;
                    Vairas.ConfirmedActions.LessonActions.Validate(action); return true;
                }
                finally { http.Request.Body.Position = 0; }
            }
            var context = Parse(http.User, 2);
            if (http.User.FindAll("aud").ToArray() is not { Length: 1 } audiences || audiences[0].Value != "vairas-" + owner) return false;
            var path = http.Request.Path.Value ?? "";
            var scope = RouteScope(owner, http.Request.Method, path);
            if (scope is null || !context.Scopes.Contains(scope, StringComparer.Ordinal)) return false;
            if (scope is ContextScope or "mcp.get_my_enrollments") return true;
            if (scope == "mcp.find_available_lessons")
            {
                var segment = path.Split('/')[4];
                return Guid.TryParse(segment, out var school) && school == context.School;
            }
            if (scope == "mcp.preview_lesson_booking")
            {
                if (!checkBody) return true; // Gateway retains streaming; owner validates body independently.
                if (http.Request.ContentLength > 65536) return false;
                http.Request.EnableBuffering(65536, 65536);
                using var json = await JsonDocument.ParseAsync(http.Request.Body, new JsonDocumentOptions { MaxDepth = 16 }, http.RequestAborted);
                http.Request.Body.Position = 0;
                return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.EnumerateObject().Count(p => p.Name == "schoolId") == 1 && json.RootElement.GetProperty("schoolId").TryGetGuid(out var school) && school == context.School;
            }
            var values = http.Request.Query["schoolId"];
            return values.Count == 1 && Guid.TryParse(values[0], out var querySchool) && querySchool == context.School;
        }
        catch (Exception e) when (e is InvalidOperationException or JsonException or FormatException or IOException or KeyNotFoundException or ArgumentException) { return false; }
    }
    internal static async Task EnforceAsync(HttpContext http, RequestDelegate next, string owner, bool checkBody = true)
    {
        if (await AllowedAsync(http, owner, checkBody)) { await next(http); return; }
        http.Response.StatusCode = 403;
        await http.Response.WriteAsJsonAsync(new { type = "about:blank", status = 403, title = "Delegated operation is not permitted.", code = "DELEGATION_DENIED", correlationId = http.TraceIdentifier }, options: (JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: http.RequestAborted);
    }
}
