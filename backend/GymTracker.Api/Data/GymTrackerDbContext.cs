using Microsoft.EntityFrameworkCore;

namespace GymTracker.Api.Data;

public sealed class GymTrackerDbContext(DbContextOptions<GymTrackerDbContext> options)
    : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ProgramTemplate> ProgramTemplates => Set<ProgramTemplate>();
    public DbSet<ProgramTemplateExercise> ProgramTemplateExercises => Set<ProgramTemplateExercise>();
    public DbSet<TrainingProgram> TrainingPrograms => Set<TrainingProgram>();
    public DbSet<ProgramVersion> ProgramVersions => Set<ProgramVersion>();
    public DbSet<ProgramVersionExercise> ProgramVersionExercises => Set<ProgramVersionExercise>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(account =>
        {
            account.ToTable("user_accounts");
            account.HasKey(user => user.FirebaseUid);
            account.Property(user => user.FirebaseUid).HasColumnName("firebase_uid").HasMaxLength(128);
            account.Property(user => user.Email).HasColumnName("email").HasMaxLength(256);
            account.Property(user => user.AuthProvider).HasColumnName("auth_provider").HasMaxLength(64).HasDefaultValue("password");
            account.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(120).HasDefaultValue(string.Empty);
            account.Property(user => user.Age).HasColumnName("age");
            account.Property(user => user.HeightCm).HasColumnName("height_cm").HasPrecision(5, 2);
            account.Property(user => user.WeightKg).HasColumnName("weight_kg").HasPrecision(5, 2);
            account.Property(user => user.ExperienceLevel).HasColumnName("experience_level").HasMaxLength(32);
            account.Property(user => user.PrimaryGoal).HasColumnName("primary_goal").HasMaxLength(64);
            account.Property(user => user.AvailableEquipment).HasColumnName("available_equipment").HasMaxLength(512);
            account.Property(user => user.DisplayTimezone).HasColumnName("display_timezone").HasMaxLength(64).HasDefaultValue("UTC");
            account.Property(user => user.OnboardingCompleted).HasColumnName("onboarding_completed").HasDefaultValue(false);
            account.Property(user => user.CreatedAtUtc).HasColumnName("created_at_utc");
            account.Property(user => user.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });

        modelBuilder.Entity<Exercise>(exercise =>
        {
            exercise.ToTable("exercises");
            exercise.HasKey(e => e.Id);
            exercise.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            exercise.Property(e => e.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            exercise.Property(e => e.PrimaryMuscleGroup).HasColumnName("primary_muscle_group").HasMaxLength(64).IsRequired();
            exercise.Property(e => e.Category).HasColumnName("category").HasMaxLength(32).IsRequired();
            exercise.Property(e => e.DefaultEquipment).HasColumnName("default_equipment").HasMaxLength(64).IsRequired();
            exercise.Property(e => e.Description).HasColumnName("description").HasMaxLength(512).IsRequired();
            exercise.HasIndex(e => e.PrimaryMuscleGroup);
            exercise.HasData(CatalogSeedData.Exercises);
        });

        modelBuilder.Entity<ProgramTemplate>(template =>
        {
            template.ToTable("program_templates");
            template.HasKey(t => t.Id);
            template.Property(t => t.Id).HasColumnName("id").HasMaxLength(64);
            template.Property(t => t.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            template.Property(t => t.Description).HasColumnName("description").HasMaxLength(512).IsRequired();
            template.Property(t => t.RecommendedExperienceLevel).HasColumnName("recommended_experience_level").HasMaxLength(32).IsRequired();
            template.HasData(CatalogSeedData.Templates);
        });

        modelBuilder.Entity<ProgramTemplateExercise>(item =>
        {
            item.ToTable("program_template_exercises");
            item.HasKey(x => x.Id);
            item.Property(x => x.Id).HasColumnName("id");
            item.Property(x => x.TemplateId).HasColumnName("template_id").HasMaxLength(64).IsRequired();
            item.Property(x => x.ExerciseId).HasColumnName("exercise_id").HasMaxLength(64).IsRequired();
            item.Property(x => x.DisplayOrder).HasColumnName("display_order");
            item.Property(x => x.TargetSets).HasColumnName("target_sets");
            item.Property(x => x.TargetReps).HasColumnName("target_reps");
            item.Property(x => x.TargetLoadKg).HasColumnName("target_load_kg").HasPrecision(6, 2);
            item.Property(x => x.RestSeconds).HasColumnName("rest_seconds");

            item.HasOne(x => x.Template)
                .WithMany(t => t.Exercises)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            item.HasOne(x => x.Exercise)
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);

            item.HasIndex(x => new { x.TemplateId, x.DisplayOrder }).IsUnique();
            item.HasData(CatalogSeedData.TemplateExercises);
        });

        modelBuilder.Entity<TrainingProgram>(program =>
        {
            program.ToTable("training_programs");
            program.HasKey(p => p.Id);
            program.Property(p => p.Id).HasColumnName("id");
            program.Property(p => p.OwnerUid).HasColumnName("owner_uid").HasMaxLength(128).IsRequired();
            program.Property(p => p.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            program.Property(p => p.Description).HasColumnName("description").HasMaxLength(512);
            program.Property(p => p.SourceTemplateId).HasColumnName("source_template_id").HasMaxLength(64);
            program.Property(p => p.CurrentVersionNumber).HasColumnName("current_version_number");
            program.Property(p => p.CurrentVersionId).HasColumnName("current_version_id");
            program.Property(p => p.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
            program.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
            program.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");

            program.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(p => p.OwnerUid)
                .OnDelete(DeleteBehavior.Cascade);

            program.HasIndex(p => p.OwnerUid);
        });

        modelBuilder.Entity<ProgramVersion>(version =>
        {
            version.ToTable("program_versions");
            version.HasKey(v => v.Id);
            version.Property(v => v.Id).HasColumnName("id");
            version.Property(v => v.ProgramId).HasColumnName("program_id");
            version.Property(v => v.VersionNumber).HasColumnName("version_number");
            version.Property(v => v.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            version.Property(v => v.Description).HasColumnName("description").HasMaxLength(512);
            version.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc");

            version.HasOne(v => v.Program)
                .WithMany(p => p.Versions)
                .HasForeignKey(v => v.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            version.HasIndex(v => new { v.ProgramId, v.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<ProgramVersionExercise>(item =>
        {
            item.ToTable("program_version_exercises");
            item.HasKey(x => x.Id);
            item.Property(x => x.Id).HasColumnName("id");
            item.Property(x => x.ProgramVersionId).HasColumnName("program_version_id");
            item.Property(x => x.ExerciseId).HasColumnName("exercise_id").HasMaxLength(64).IsRequired();
            item.Property(x => x.DisplayOrder).HasColumnName("display_order");
            item.Property(x => x.TargetSets).HasColumnName("target_sets");
            item.Property(x => x.TargetReps).HasColumnName("target_reps");
            item.Property(x => x.TargetLoadKg).HasColumnName("target_load_kg").HasPrecision(6, 2);
            item.Property(x => x.RestSeconds).HasColumnName("rest_seconds");

            item.HasOne(x => x.ProgramVersion)
                .WithMany(v => v.Exercises)
                .HasForeignKey(x => x.ProgramVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            item.HasOne(x => x.Exercise)
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);

            item.HasIndex(x => new { x.ProgramVersionId, x.DisplayOrder }).IsUnique();
        });
    }
}

public sealed class UserAccount
{
    public required string FirebaseUid { get; init; }
    public string? Email { get; set; }
    public string AuthProvider { get; set; } = "password";
    public string DisplayName { get; set; } = string.Empty;
    public int? Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? PrimaryGoal { get; set; }
    public string? AvailableEquipment { get; set; }
    public string DisplayTimezone { get; set; } = "UTC";
    public bool OnboardingCompleted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class Exercise
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string PrimaryMuscleGroup { get; set; }
    public required string Category { get; set; }
    public required string DefaultEquipment { get; set; }
    public required string Description { get; set; }
}

public sealed class ProgramTemplate
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string RecommendedExperienceLevel { get; set; }
    public List<ProgramTemplateExercise> Exercises { get; set; } = [];
}

public sealed class ProgramTemplateExercise
{
    public int Id { get; init; }
    public required string TemplateId { get; init; }
    public required string ExerciseId { get; init; }
    public int DisplayOrder { get; set; }
    public int TargetSets { get; set; }
    public int TargetReps { get; set; }
    public decimal? TargetLoadKg { get; set; }
    public int? RestSeconds { get; set; }

    public ProgramTemplate? Template { get; set; }
    public Exercise? Exercise { get; set; }
}

public sealed class TrainingProgram
{
    public Guid Id { get; init; }
    public required string OwnerUid { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? SourceTemplateId { get; set; }
    public int CurrentVersionNumber { get; set; }
    public Guid CurrentVersionId { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<ProgramVersion> Versions { get; set; } = [];
}

public sealed class ProgramVersion
{
    public Guid Id { get; init; }
    public Guid ProgramId { get; init; }
    public int VersionNumber { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public TrainingProgram? Program { get; set; }
    public List<ProgramVersionExercise> Exercises { get; set; } = [];
}

public sealed class ProgramVersionExercise
{
    public Guid Id { get; init; }
    public Guid ProgramVersionId { get; init; }
    public required string ExerciseId { get; init; }
    public int DisplayOrder { get; init; }
    public int TargetSets { get; init; }
    public int TargetReps { get; init; }
    public decimal? TargetLoadKg { get; init; }
    public int? RestSeconds { get; init; }

    public ProgramVersion? ProgramVersion { get; set; }
    public Exercise? Exercise { get; set; }
}
