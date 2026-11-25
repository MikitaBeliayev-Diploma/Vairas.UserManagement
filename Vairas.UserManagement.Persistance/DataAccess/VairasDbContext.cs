using Microsoft.EntityFrameworkCore;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Persistance.DataAccess;

public sealed class VairasDbContext(DbContextOptions<VairasDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> Users => Set<UserProfile>();
    public DbSet<StudentProfile> Students => Set<StudentProfile>();
    public DbSet<TeacherProfile> Teachers => Set<TeacherProfile>();
    public DbSet<AdministratorProfile> Administrators => Set<AdministratorProfile>();
    public DbSet<ProfileOperation> Operations => Set<ProfileOperation>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => ModelConfiguration.Configure(modelBuilder);
}

