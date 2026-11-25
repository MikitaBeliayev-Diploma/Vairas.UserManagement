using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Vairas.UserManagement.Configuration;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Vairas User Management API";
        document.Info.Version = "1.0.1";
        document.Info.Description = "Global profiles and idempotent commands. Internal operations require a workload JWT. " +
            "Delegated and platform mutations additionally require a user JWT in X-User-Authorization. " +
            "Mutations return operation receipts; fetch current resource state separately.";
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["UserBearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" },
            ["WorkloadBearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" },
            ["DelegatedUser"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header,
                Name = "X-User-Authorization", Description = "Bearer <user JWT>, validated independently of the workload token." }
        };
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas["ApiProblem"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object, Required = new HashSet<string> { "title", "status", "code", "correlationId" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        foreach (var (path, item) in document.Paths)
        {
            // Probe and discovery metadata have their own authentication and content types.
            if (!path.StartsWith("/api/v1/users/me", StringComparison.Ordinal) &&
                !path.StartsWith("/internal/v1/", StringComparison.Ordinal)) continue;

            foreach (var (method, operation) in item.Operations!)
            {
                var requirement = new OpenApiSecurityRequirement();
                var internalPath = path.StartsWith("/internal/", StringComparison.Ordinal);
                requirement[new OpenApiSecuritySchemeReference(internalPath ? "WorkloadBearer" : "UserBearer", document)] = [];
                if (internalPath && path != "/internal/v1/profiles/assistant-actor" && (path.Contains("/delegated/", StringComparison.Ordinal) || method == HttpMethod.Post))
                    requirement[new OpenApiSecuritySchemeReference("DelegatedUser", document)] = [];
                operation.Security = [requirement];
                if (internalPath)
                {
                    var scope = path.Contains("/delegated/", StringComparison.Ordinal) ? "profiles.mcp.write"
                        : path.EndsWith("/status", StringComparison.Ordinal) ? "profiles.status.read"
                        : path.EndsWith("/contact", StringComparison.Ordinal) ? "profiles.contact.read"
                        : path.EndsWith("/deactivate", StringComparison.Ordinal) ? "profiles.deactivate"
                        : path.EndsWith("/bootstrap", StringComparison.Ordinal) ? "profiles.bootstrap" : "profiles.binding.read";
                    operation.Description = (operation.Description ?? "") + " Required workload scope and local client allowlist: " + scope + ".";
                }

                operation.Responses ??= new();
                foreach (var response in operation.Responses.Values)
                    response.Content?.Remove("text/plain");

                if (path.EndsWith("/deactivate", StringComparison.Ordinal))
                    operation.Responses["200"] = ReceiptResponse(document, "Deactivated or already inactive; operation receipt.");
                if (path.EndsWith("/bootstrap", StringComparison.Ordinal))
                {
                    operation.Responses["201"] = ReceiptResponse(document, "Administrator profile created; operation receipt and Location header.");
                    operation.Responses["200"] = ReceiptResponse(document, "Existing administrator profile reused (ALREADY_EXISTS); operation receipt.");
                }

                foreach (var status in new[] { "400", "401", "403", "404", "409", "415", "422", "500", "503" })
                    operation.Responses.Remove(status);
                foreach (var status in ErrorStatuses(path, method).Order(StringComparer.Ordinal))
                    operation.Responses[status] = new OpenApiResponse
                    {
                        Description = ErrorDescription(status, path, method),
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference("ApiProblem", document) }
                        }
                    };
            }
        }
        return Task.CompletedTask;
    }

    private static OpenApiResponse ReceiptResponse(OpenApiDocument document, string description) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new() { Schema = new OpenApiSchemaReference("OperationReceipt", document) },
            ["text/json"] = new() { Schema = new OpenApiSchemaReference("OperationReceipt", document) }
        }
    };

    private static IEnumerable<string> ErrorStatuses(string path, HttpMethod method)
    {
        yield return "401";
        yield return "403";
        yield return "500";
        yield return "503";
        if (method == HttpMethod.Get || path.Contains("{role}", StringComparison.Ordinal) ||
            path.EndsWith("/deactivate", StringComparison.Ordinal) || path.EndsWith("/bootstrap", StringComparison.Ordinal))
            yield return "404";
        if (method == HttpMethod.Get) yield break;

        yield return "400";
        yield return "409";
        yield return "415";
        if (path.Contains("/delegated/", StringComparison.Ordinal) || path.EndsWith("/bootstrap", StringComparison.Ordinal))
            yield return "422";
    }

    private static string ErrorDescription(string status, string path, HttpMethod method)
    {
        var workloadRead = method == HttpMethod.Get && path.StartsWith("/internal/", StringComparison.Ordinal) &&
            !path.Contains("/delegated/", StringComparison.Ordinal);
        return status switch
        {
            "400" => "Invalid JSON, required properties or command arguments (VALIDATION_FAILED).",
            "401" => workloadRead ? "Workload authentication required (AUTHENTICATION_REQUIRED)."
                : "Authentication required or user identity/session invalid (AUTHENTICATION_REQUIRED, INVALID_IDENTITY, SESSION_INVALID).",
            "403" => workloadRead ? "Workload scope or local client allowlist rejected (ACCESS_DENIED)."
                : "Access policy or current business authority rejected (ACCESS_DENIED).",
            "404" => "Profile, target user or role not found (NOT_FOUND).",
            "409" => "Command identity, resource uniqueness or expected revision conflict; applicable codes depend on the command.",
            "415" => "Unsupported request Content-Type (HTTP_ERROR); send a documented JSON media type.",
            "422" => path.EndsWith("/bootstrap", StringComparison.Ordinal)
                ? "The target user is inactive (USER_INACTIVE)."
                : "Exact MCP confirmation is missing, invalid or expired (CONFIRMATION_REQUIRED, CONFIRMATION_INVALID, CONFIRMATION_EXPIRED).",
            "500" => "Unexpected failure (INTERNAL_ERROR); preserve correlation ID and reconcile mutations before retrying.",
            "503" => workloadRead ? "Owned database unavailable (DATABASE_UNAVAILABLE)."
                : "Authoritative dependency or owned database unavailable (DEPENDENCY_UNAVAILABLE, DATABASE_UNAVAILABLE).",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
}
