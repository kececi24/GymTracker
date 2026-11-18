using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workout_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    performed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    program_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_workout_records_program_versions_program_version_id",
                        column: x => x.program_version_id,
                        principalTable: "program_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workout_records_user_accounts_owner_uid",
                        column: x => x.owner_uid,
                        principalTable: "user_accounts",
                        principalColumn: "firebase_uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    exercise_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    repetitions = table.Column<int>(type: "integer", nullable: false),
                    load_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_sets", x => x.id);
                    table.CheckConstraint("ck_workout_sets_load_kg", "load_kg IS NULL OR load_kg BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_workout_sets_repetitions", "repetitions BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_workout_sets_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workout_sets_workout_records_workout_id",
                        column: x => x.workout_id,
                        principalTable: "workout_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workout_records_owner_uid_performed_at_utc",
                table: "workout_records",
                columns: new[] { "owner_uid", "performed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_workout_records_program_version_id",
                table: "workout_records",
                column: "program_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_sets_exercise_id",
                table: "workout_sets",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_sets_workout_id_display_order",
                table: "workout_sets",
                columns: new[] { "workout_id", "display_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workout_sets");

            migrationBuilder.DropTable(
                name: "workout_records");
        }
    }
}
