using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymTracker.Api.Data;

public sealed class ScheduledSession
{
    public Guid Id { get; init; }
    public required string OwnerUid { get; init; }
    public DateTimeOffset StartAtUtc { get; set; }
    public required string DisplayTimezone { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
}

internal sealed class ScheduledSessionConfiguration : IEntityTypeConfiguration<ScheduledSession>
{
    public void Configure(EntityTypeBuilder<ScheduledSession> session)
    {
        session.ToTable("scheduled_sessions");
        session.HasKey(s => s.Id);
        session.Property(s => s.Id).HasColumnName("id");
        session.Property(s => s.OwnerUid).HasColumnName("owner_uid").HasMaxLength(128);
        session.Property(s => s.StartAtUtc).HasColumnName("start_at_utc");
        session.Property(s => s.DisplayTimezone).HasColumnName("display_timezone").HasMaxLength(64);
        session.Property(s => s.CancelledAtUtc).HasColumnName("cancelled_at_utc");
        session.HasOne<UserAccount>().WithMany().HasForeignKey(s => s.OwnerUid)
            .OnDelete(DeleteBehavior.Cascade);
        session.HasIndex(s => new { s.OwnerUid, s.StartAtUtc });
    }
}
