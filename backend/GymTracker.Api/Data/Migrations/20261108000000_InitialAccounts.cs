using GymTracker.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GymTracker.Api.Data.Migrations;

[DbContext(typeof(GymTrackerDbContext))]
[Migration("20261108000000_InitialAccounts")]
public sealed class InitialAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_accounts",
            columns: table => new
            {
                firebase_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_user_accounts", user => user.firebase_uid));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("user_accounts");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
