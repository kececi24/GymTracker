using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymTracker.Api.Data;

public sealed class WorkoutRecord
{
    public Guid Id { get; init; }
    public required string OwnerUid { get; init; }
    public DateTimeOffset PerformedAtUtc { get; set; }
    public Guid? ProgramVersionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<WorkoutSet> Sets { get; set; } = [];
}

public sealed class WorkoutSet
{
    public Guid Id { get; init; }
    public Guid WorkoutId { get; init; }
    public int DisplayOrder { get; init; }
    public required string ExerciseId { get; set; }
    public int Repetitions { get; set; }
    public decimal? LoadKg { get; set; }
}

internal sealed class WorkoutRecordConfiguration : IEntityTypeConfiguration<WorkoutRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutRecord> record)
    {
        record.ToTable("workout_records");
        record.HasKey(w => w.Id);
        record.Property(w => w.Id).HasColumnName("id");
        record.Property(w => w.OwnerUid).HasColumnName("owner_uid").HasMaxLength(128).IsRequired();
        record.Property(w => w.PerformedAtUtc).HasColumnName("performed_at_utc");
        record.Property(w => w.ProgramVersionId).HasColumnName("program_version_id");
        record.Property(w => w.CreatedAtUtc).HasColumnName("created_at_utc");
        record.Property(w => w.UpdatedAtUtc).HasColumnName("updated_at_utc");
        record.HasOne<UserAccount>().WithMany().HasForeignKey(w => w.OwnerUid)
            .OnDelete(DeleteBehavior.Cascade);
        // Program edits and archival must never change or delete historical references.
        record.HasOne<ProgramVersion>().WithMany().HasForeignKey(w => w.ProgramVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        record.HasIndex(w => new { w.OwnerUid, w.PerformedAtUtc });
    }
}

internal sealed class WorkoutSetConfiguration : IEntityTypeConfiguration<WorkoutSet>
{
    public void Configure(EntityTypeBuilder<WorkoutSet> set)
    {
        set.ToTable("workout_sets", table =>
        {
            table.HasCheckConstraint("ck_workout_sets_repetitions", "repetitions BETWEEN 1 AND 500");
            table.HasCheckConstraint("ck_workout_sets_load_kg", "load_kg IS NULL OR load_kg BETWEEN 0 AND 1000");
        });
        set.HasKey(s => s.Id);
        set.Property(s => s.Id).HasColumnName("id");
        set.Property(s => s.WorkoutId).HasColumnName("workout_id");
        set.Property(s => s.DisplayOrder).HasColumnName("display_order");
        set.Property(s => s.ExerciseId).HasColumnName("exercise_id").HasMaxLength(64).IsRequired();
        set.Property(s => s.Repetitions).HasColumnName("repetitions");
        set.Property(s => s.LoadKg).HasColumnName("load_kg").HasPrecision(6, 2);
        set.HasOne<WorkoutRecord>().WithMany(w => w.Sets).HasForeignKey(s => s.WorkoutId)
            .OnDelete(DeleteBehavior.Cascade);
        set.HasOne<Exercise>().WithMany().HasForeignKey(s => s.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
        set.HasIndex(s => new { s.WorkoutId, s.DisplayOrder }).IsUnique();
    }
}
