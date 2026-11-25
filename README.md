# Vairas User Management

Owner of global personal profiles (student, teacher, administrator), audited idempotent profile commands and global
user deactivation (ASP.NET Core 10, PostgreSQL, EF Core/Npgsql). Authentication owns credentials and tokens; School
Management owns memberships and invitation authority.

## Projects

| Project | Responsibility |
|---|---|
| `Vairas.UserManagement` | HTTP API: controllers, `Configuration/Configure*`, security, dependency clients, outbox worker |
| `Vairas.UserManagement.Business` | Application services, validation, dependency contracts |
| `Vairas.UserManagement.Domain` | Profiles, operation outcomes, outbox records |
| `Vairas.UserManagement.Persistance` | PostgreSQL mappings, transactions, advisory locks, migrations |
| `Vairas.UserManagement.Tests` | NUnit tests with PostgreSQL Testcontainers and loopback HTTP doubles |

## Dependencies

PostgreSQL 17, Authentication (token issuer, session validation), School Management (invitation authority),
optionally a Service Bus broker for the `UserDeactivated` outbox. Local origin `https://localhost:7001`.

## Essential configuration

Options are bound and validated at startup (`Configuration/ConfigureOptions.cs`). Use `__` in environment variables.

| Section | Notes |
|---|---|
| `ConnectionStrings:UserManagement` | restricted runtime role |
| `Authentication` | `Authority`, `Issuer`, `UserAudience`, `WorkloadAudience`, `WorkloadClients` allowlist |
| `Dependencies` | absolute origins, `TokenEndpoint`, `ClientId`, `ClientSecret`, `Scope` |
| `ProfileAccess` | `PlatformOperatorIdentityIds`, `BootstrapWorkloads` |
| `Outbox` | `Enabled`, `ConnectionString`, `TopicName`, `PollSeconds`; a disabled outbox needs no credentials |

## Run locally

```powershell
dotnet run --project Vairas.UserManagement --launch-profile http
```

The complete local stack is started from `../local/Start.ps1`. Database roles and grants are in `database/*.sql`;
migrations are applied with `dotnet ef database update` by the migration role, never on startup.

## Tests

```powershell
dotnet test Vairas.UserManagement.slnx          # Docker required for PostgreSQL tests
./scripts/verify.ps1 -SkipIntegration           # unit tests with the coverage gate
```

API export: `api/`. Extended notes, validation reports and evidence: `../../Documentation/Backend/Vairas.UserManagement/`.
