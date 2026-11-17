using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GymTracker.Api.Data.Migrations;

[DbContext(typeof(GymTrackerDbContext))]
public sealed class GymTrackerDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("GymTracker.Api.Data.UserAccount", account =>
        {
            account.Property<string>("FirebaseUid")
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("firebase_uid");

            account.HasKey("FirebaseUid");
            account.ToTable("user_accounts");
        });
    }
}
