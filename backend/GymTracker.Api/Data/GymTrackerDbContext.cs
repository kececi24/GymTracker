using Microsoft.EntityFrameworkCore;

namespace GymTracker.Api.Data;

public sealed class GymTrackerDbContext(DbContextOptions<GymTrackerDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(account =>
        {
            account.ToTable("user_accounts");
            account.HasKey(user => user.FirebaseUid);
            account.Property(user => user.FirebaseUid).HasColumnName("firebase_uid").HasMaxLength(128);
        });
    }
}

public sealed class UserAccount
{
    public required string FirebaseUid { get; init; }
}
