using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Persistance.DataAccess;

namespace Vairas.UserManagement.Persistance;

public sealed class ProfileStore(VairasDbContext db) : IProfileStore
{
    public async Task<IProfileTransaction> BeginAsync(CancellationToken ct) => new Transaction(await db.Database.BeginTransactionAsync(ct));
    public async Task LockAsync(string key, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A transaction is required.");
        var id = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({id})", ct);
    }
    public Task<UserProfile?> FindUserAsync(Guid id, CancellationToken ct) => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id, ct);
    public Task<UserProfile?> FindIdentityAsync(Guid identityId, CancellationToken ct) => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.IdentityId == identityId, ct);
    public Task<ProfileOperation?> FindOperationAsync(Guid id, CancellationToken ct) => db.Operations.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == id, ct);
    public Task<Guid?> FindRoleIdAsync(ProfileKind kind, Guid userId, CancellationToken ct) => kind switch
    {
        ProfileKind.Student => db.Students.Where(x => x.UserId == userId).Select(x => (Guid?)x.StudentId).SingleOrDefaultAsync(ct),
        ProfileKind.Teacher => db.Teachers.Where(x => x.UserId == userId).Select(x => (Guid?)x.TeacherId).SingleOrDefaultAsync(ct),
        ProfileKind.Administrator => db.Administrators.Where(x => x.UserId == userId).Select(x => (Guid?)x.AdministratorId).SingleOrDefaultAsync(ct),
        _ => throw ServiceErrors.Missing()
    };
    public Task<RoleProfileResult?> FindRoleAsync(ProfileKind kind, Guid id, CancellationToken ct) => kind switch
    {
        ProfileKind.Student => (from role in db.Students join user in db.Users on role.UserId equals user.UserId
            where role.StudentId == id select new RoleProfileResult(kind, id, user.UserId, user.Status == UserStatus.Active)).SingleOrDefaultAsync(ct),
        ProfileKind.Teacher => (from role in db.Teachers join user in db.Users on role.UserId equals user.UserId
            where role.TeacherId == id select new RoleProfileResult(kind, id, user.UserId, user.Status == UserStatus.Active)).SingleOrDefaultAsync(ct),
        ProfileKind.Administrator => (from role in db.Administrators join user in db.Users on role.UserId equals user.UserId
            where role.AdministratorId == id select new RoleProfileResult(kind, id, user.UserId, user.Status == UserStatus.Active)).SingleOrDefaultAsync(ct),
        _ => throw ServiceErrors.Missing()
    };
    public void Add(UserProfile profile) => db.Users.Add(profile);
    public void Update(UserProfile profile)
    {
        db.Users.Attach(profile);
        var entry = db.Entry(profile);
        foreach (var name in new[] { nameof(UserProfile.FirstName), nameof(UserProfile.LastName), nameof(UserProfile.EmailAddress),
                     nameof(UserProfile.PhoneNumber), nameof(UserProfile.Status), nameof(UserProfile.Revision), nameof(UserProfile.UpdatedAt) })
            entry.Property(name).IsModified = true;
        entry.Property(x => x.Revision).OriginalValue = profile.Revision - 1;
    }
    public void Add(ProfileOperation operation) => db.Operations.Add(operation);
    public void Add(OutboxMessage message) => db.Outbox.Add(message);
    public void AddRole(ProfileKind kind, Guid id, Guid userId, DateTimeOffset now)
    {
        switch (kind)
        {
            case ProfileKind.Student: db.Students.Add(new() { StudentId = id, UserId = userId, CreatedAt = now }); break;
            case ProfileKind.Teacher: db.Teachers.Add(new() { TeacherId = id, UserId = userId, CreatedAt = now }); break;
            case ProfileKind.Administrator: db.Administrators.Add(new() { AdministratorId = id, UserId = userId, CreatedAt = now }); break;
            default: throw ServiceErrors.Missing();
        }
    }
    public Task SaveAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => db.SaveChangesAsync(ct));
    private sealed class Transaction(IDbContextTransaction tx) : IProfileTransaction
    {
        public Task CommitAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => tx.CommitAsync(ct));
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
