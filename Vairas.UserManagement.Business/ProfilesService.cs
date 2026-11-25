using System.Text.Json;
using FluentValidation;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed class ProfilesService(IProfileStore store, IAuthSessionValidator sessions,
    ISchoolAuthorityClient schools, IConfirmationClient confirmations, ProfileAccessOptions access,
    TimeProvider clock, IValidator<ProfileCommand> validator) : IProfilesService
{
    public async Task<UserProfileResult> GetMyAsync(ActorContext actor, CancellationToken ct)
    {
        var validUntil = await sessions.ValidateAsync(actor, ct);
        var user = await store.FindIdentityAsync(actor.IdentityId, ct) ?? throw ServiceErrors.Missing();
        EnsureActive(user);
        var result = new UserProfileResult(user.UserId, user.FirstName, user.LastName, user.EmailAddress, user.PhoneNumber,
            user.Status, user.Revision, user.CreatedAt, user.UpdatedAt,
            await store.FindRoleIdAsync(ProfileKind.Student, user.UserId, ct),
            await store.FindRoleIdAsync(ProfileKind.Teacher, user.UserId, ct),
            await store.FindRoleIdAsync(ProfileKind.Administrator, user.UserId, ct));
        if (validUntil <= clock.GetUtcNow()) throw ServiceErrors.Dependency();
        return result;
    }

    public async Task<RoleProfileResult> GetMyRoleAsync(ProfileKind kind, ActorContext actor, CancellationToken ct)
    {
        var user = await GetMyAsync(actor, ct);
        var id = kind switch { ProfileKind.Student => user.StudentId, ProfileKind.Teacher => user.TeacherId,
            ProfileKind.Administrator => user.AdministratorId, _ => throw ServiceErrors.Missing() };
        return id.HasValue ? new(kind, id.Value, user.UserId, true) : throw ServiceErrors.Missing();
    }

    public Task<UserProfile?> FindUserAsync(Guid userId, CancellationToken ct) => store.FindUserAsync(userId, ct);
    public async Task<RoleProfileResult> FindRoleAsync(ProfileKind kind, Guid profileId, CancellationToken ct) =>
        await store.FindRoleAsync(kind, profileId, ct) ?? throw ServiceErrors.Missing();

    public async Task<UserRoleProfilesResult> FindRoleProfilesAsync(Guid userId, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, ct) ?? throw ServiceErrors.Missing();
        return new(user.UserId, user.Status, user.Revision, await store.FindRoleIdAsync(ProfileKind.Student, userId, ct),
            await store.FindRoleIdAsync(ProfileKind.Teacher, userId, ct), await store.FindRoleIdAsync(ProfileKind.Administrator, userId, ct));
    }

    public async Task<OperationReceipt> ExecuteAsync(ProfileCommand command, ActorContext actor, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        var validUntil = await sessions.ValidateAsync(actor, ct);
        var preliminaryActor = await store.FindIdentityAsync(actor.IdentityId, ct);
        if (preliminaryActor is not null) EnsureActive(preliminaryActor);
        if (command.Kind != ProfileCommandKind.CreateUser && preliminaryActor is null) throw ServiceErrors.Forbidden();
        var privileged = command.Kind is ProfileCommandKind.Deactivate or ProfileCommandKind.BootstrapAdministrator;
        if (privileged && !access.PlatformOperatorIdentityIds.Contains(actor.IdentityId)) throw ServiceErrors.Forbidden();
        if (command.Kind == ProfileCommandKind.BootstrapAdministrator &&
            (actor.Workload is null || !access.BootstrapWorkloads.Contains(actor.Workload, StringComparer.Ordinal)))
            throw ServiceErrors.Forbidden();

        var fingerprint = CommandFingerprint.Compute(command, actor);
        var previous = await store.FindOperationAsync(command.OperationId, ct);
        if (previous is not null) EnsureSame(previous, fingerprint);
        var authorityBasis = $"self:{actor.IdentityId:D}";
        SchoolAuthorityResult? authority = null;
        if (command.Kind is ProfileCommandKind.CreateTeacher or ProfileCommandKind.CreateAdministrator or ProfileCommandKind.BootstrapAdministrator)
        {
            var role = command.Kind == ProfileCommandKind.CreateTeacher ? ProfileKind.Teacher : ProfileKind.Administrator;
            var request = new SchoolAuthorityRequest(preliminaryActor!.UserId,
                command.TargetUserId ?? preliminaryActor.UserId, command.SchoolId!.Value, command.InvitationId,
                role, command.Kind == ProfileCommandKind.BootstrapAdministrator);
            authority = await schools.ValidateAsync(request, ct);
            if (!authority.Allowed || authority.ActorUserId != request.ActorUserId || authority.TargetUserId != request.TargetUserId ||
                authority.SchoolId != request.SchoolId || authority.InvitationId != request.InvitationId || authority.Role != request.Role ||
                authority.Bootstrap != request.Bootstrap || string.IsNullOrWhiteSpace(authority.AuthorityBasis) || authority.AuthorityBasis.Length > 250)
                throw ServiceErrors.Forbidden();
            authorityBasis = authority.AuthorityBasis;
        }
        else if (privileged) authorityBasis = $"platform-operator:{actor.IdentityId:D}";

        ConfirmationResult? confirmation = null;
        if (actor.Channel == OperationChannel.MCP && previous is null)
        {
            if (command.ConfirmationId is null) throw new UnprocessableEntityException("CONFIRMATION_REQUIRED", "Exact confirmation is required.");
            var request = new ConfirmationRequest(command.ConfirmationId.Value, actor.IdentityId,
                command.OperationId, fingerprint, command.SchoolId, command.Kind.ToString());
            confirmation = await confirmations.ValidateAsync(request, ct);
            if (!confirmation.Allowed || confirmation.ConfirmationId != request.ConfirmationId ||
                confirmation.IdentityId != actor.IdentityId || confirmation.OperationId != command.OperationId ||
                confirmation.RequestFingerprint != fingerprint || confirmation.SchoolId != command.SchoolId ||
                confirmation.CommandName != request.CommandName || confirmation.ConfirmedAt > clock.GetUtcNow() ||
                confirmation.ExpiresAt <= confirmation.ConfirmedAt)
                throw new UnprocessableEntityException("CONFIRMATION_INVALID", "Confirmation does not match the command.");
        }

        await using var tx = await store.BeginAsync(ct);
        await store.LockAsync($"operation:{command.OperationId:D}", ct);
        var lockKeys = new List<string> { $"identity:{actor.IdentityId:D}" };
        if (preliminaryActor is not null) lockKeys.Add($"user:{preliminaryActor.UserId:D}");
        if (command.TargetUserId.HasValue) lockKeys.Add($"user:{command.TargetUserId:D}");
        if (authority?.IssuerUserId is Guid issuerUserId && issuerUserId != Guid.Empty)
            lockKeys.Add($"user:{issuerUserId:D}");
        foreach (var key in lockKeys.Distinct().Order(StringComparer.Ordinal)) await store.LockAsync(key, ct);
        var currentActor = await store.FindIdentityAsync(actor.IdentityId, ct);
        if (currentActor is not null) EnsureActive(currentActor);
        if (command.Kind != ProfileCommandKind.CreateUser && currentActor is null) throw ServiceErrors.Forbidden();
        if (validUntil <= clock.GetUtcNow()) throw ServiceErrors.Dependency();
        // A saved receipt still requires a fresh admission from School for this request.
        // Check after waiting for locks, before either replay or a new mutation.
        var instant = clock.GetUtcNow();
        if (authority is not null && authority.ValidUntil <= instant) throw ServiceErrors.Dependency();

        previous = await store.FindOperationAsync(command.OperationId, ct);
        if (previous is not null)
        {
            EnsureSame(previous, fingerprint);
            await tx.CommitAsync(ct);
            return Receipt(previous);
        }
        // PostgreSQL timestamptz stores microseconds. First response and replay must agree exactly.
        var now = instant.AddTicks(-(instant.Ticks % 10));
        if (actor.Channel == OperationChannel.MCP && (confirmation is null || confirmation.ExpiresAt <= now))
            throw new UnprocessableEntityException("CONFIRMATION_EXPIRED", "A fresh confirmation is required.");

        var user = command.TargetUserId.HasValue ? await store.FindUserAsync(command.TargetUserId.Value, ct) : currentActor;
        var userId = user?.UserId ?? (command.Kind == ProfileCommandKind.CreateUser ? Guid.NewGuid() : command.TargetUserId!.Value);
        var targetId = userId;
        var targetKind = "UserProfile";
        long? resultingRevision = null;
        string code = "APPLIED";
        var outcome = OperationOutcome.Applied;

        if (command.Kind != ProfileCommandKind.CreateUser && user is null)
            (outcome, code) = (OperationOutcome.Rejected, "NOT_FOUND");
        else if (command.ExpectedRevision.HasValue && command.ExpectedRevision != user?.Revision)
            (outcome, code) = (OperationOutcome.Rejected, "REVISION_CONFLICT");
        else if (command.Kind == ProfileCommandKind.CreateUser)
        {
            if (user is not null) (outcome, code) = (OperationOutcome.Rejected, "PROFILE_EXISTS");
            else
            {
                user = new UserProfile { UserId = userId, IdentityId = actor.IdentityId,
                    FirstName = command.FirstName!, LastName = command.LastName!, EmailAddress = command.EmailAddress,
                    PhoneNumber = command.PhoneNumber, CreatedAt = now, UpdatedAt = now };
                store.Add(user);
                resultingRevision = 1;
            }
        }
        else if (command.Kind == ProfileCommandKind.UpdateUser)
        {
            user!.FirstName = command.FirstName!;
            user.LastName = command.LastName!;
            user.EmailAddress = command.EmailAddress;
            user.PhoneNumber = command.PhoneNumber;
            user.UpdatedAt = now;
            user.Revision++;
            store.Update(user);
            resultingRevision = user.Revision;
        }
        else if (command.Kind == ProfileCommandKind.Deactivate)
        {
            if (user!.Status == UserStatus.Active)
            {
                user.Status = UserStatus.Inactive;
                user.Revision++;
                user.UpdatedAt = now;
                store.Update(user);
                AddUserDeactivatedEvent(userId, user.Revision, command.OperationId, actor, now);
            }
            else code = "ALREADY_INACTIVE";
            resultingRevision = user.Revision;
        }
        else
        {
            if (user!.Status != UserStatus.Active) (outcome, code) = (OperationOutcome.Rejected, "USER_INACTIVE");
            else
            {
                var kind = command.Kind switch { ProfileCommandKind.CreateStudent => ProfileKind.Student,
                    ProfileCommandKind.CreateTeacher => ProfileKind.Teacher, _ => ProfileKind.Administrator };
                targetKind = kind + "Profile";
                var existingId = await store.FindRoleIdAsync(kind, userId, ct);
                targetId = existingId ?? Guid.NewGuid();
                if (existingId.HasValue) code = "ALREADY_EXISTS";
                else
                {
                    // School validates its local invitation basis before this transaction.
                    // User Management owns the issuer's primary activity and serializes it
                    // with deactivation using the same user lock before first creation.
                    if (authority is not null)
                    {
                        var issuer = authority.IssuerUserId is Guid issuerId && issuerId != Guid.Empty
                            ? await store.FindUserAsync(issuerId, ct) : null;
                        if (issuer is null) throw ServiceErrors.Forbidden();
                        EnsureActive(issuer);
                    }
                    store.AddRole(kind, targetId, userId, now);
                }
            }
        }
        var operation = new ProfileOperation { OperationId = command.OperationId,
            ActorUserId = currentActor?.UserId ?? userId, ActorService = actor.Workload,
            RequestingSchoolId = command.SchoolId, AuthorityBasis = authorityBasis, Channel = actor.Channel,
            CommandName = command.Kind.ToString(), TargetKind = targetKind, TargetId = targetId,
            ExpectedRevision = command.ExpectedRevision, ResultingRevision = resultingRevision,
            RequestFingerprint = fingerprint, Outcome = outcome, OutcomeCode = code, RecordedAt = now,
            Reason = command.Reason, ConfirmationId = confirmation?.ConfirmationId, ConfirmedAt = confirmation?.ConfirmedAt };
        store.Add(operation);
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return Receipt(operation);
    }

    private void AddUserDeactivatedEvent(Guid userId, long revision, Guid operationId, ActorContext actor, DateTimeOffset now)
    {
        var eventId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            headers = new { eventId, eventType = "UserDeactivated", eventVersion = 1, occurredAt = now,
                producer = "user", correlationId = actor.CorrelationId, causationId = operationId.ToString(),
                aggregateId = userId, aggregateVersion = revision, traceparent = actor.TraceParent },
            payload = new { userId, deactivatedAt = now }
        }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        store.Add(new OutboxMessage { EventId = eventId, AggregateId = userId, AggregateVersion = revision,
            EventType = "UserDeactivated", Body = body, CorrelationId = actor.CorrelationId,
            TraceParent = actor.TraceParent, OccurredAt = now, NextAttemptAt = now });
    }

    private static void EnsureActive(UserProfile user)
    {
        if (user.Status != UserStatus.Active) throw ServiceErrors.Forbidden();
    }
    private static void EnsureSame(ProfileOperation operation, string fingerprint)
    {
        if (operation.RequestFingerprint != fingerprint)
            throw new ConflictException("IDEMPOTENCY_KEY_REUSED", "The operation ID is already in use.");
    }
    private static OperationReceipt Receipt(ProfileOperation operation) => new(operation.OperationId, operation.Outcome,
        operation.OutcomeCode, operation.TargetKind, operation.TargetId, operation.ResultingRevision, operation.RecordedAt);
}
