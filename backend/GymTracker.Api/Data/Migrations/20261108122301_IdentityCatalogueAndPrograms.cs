using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GymTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IdentityCatalogueAndPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "age",
                table: "user_accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "auth_provider",
                table: "user_accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "password");

            migrationBuilder.AddColumn<string>(
                name: "available_equipment",
                table: "user_accounts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "user_accounts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "display_timezone",
                table: "user_accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "user_accounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "experience_level",
                table: "user_accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "height_cm",
                table: "user_accounts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "onboarding_completed",
                table: "user_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "primary_goal",
                table: "user_accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<decimal>(
                name: "weight_kg",
                table: "user_accounts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    primary_muscle_group = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    default_equipment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "program_templates",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    recommended_experience_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_programs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_template_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    current_version_number = table.Column<int>(type: "integer", nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_programs", x => x.id);
                    table.ForeignKey(
                        name: "FK_training_programs_user_accounts_owner_uid",
                        column: x => x.owner_uid,
                        principalTable: "user_accounts",
                        principalColumn: "firebase_uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_template_exercises",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    exercise_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    target_sets = table.Column<int>(type: "integer", nullable: false),
                    target_reps = table.Column<int>(type: "integer", nullable: false),
                    target_load_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    rest_seconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_template_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_program_template_exercises_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_template_exercises_program_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "program_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_program_versions_training_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "training_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_version_exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    target_sets = table.Column<int>(type: "integer", nullable: false),
                    target_reps = table.Column<int>(type: "integer", nullable: false),
                    target_load_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    rest_seconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_version_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_program_version_exercises_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_version_exercises_program_versions_program_version_~",
                        column: x => x.program_version_id,
                        principalTable: "program_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "exercises",
                columns: new[] { "id", "category", "default_equipment", "description", "name", "primary_muscle_group" },
                values: new object[,]
                {
                    { "barbell-back-squat", "Resistance", "Barbell", "Compound lower-body squat performed with a barbell across the upper back.", "Barbell Back Squat", "Legs" },
                    { "barbell-bench-press", "Resistance", "Barbell", "Horizontal pressing movement targeting the pectorals, anterior deltoids, and triceps.", "Barbell Bench Press", "Chest" },
                    { "barbell-bent-over-row", "Resistance", "Barbell", "Horizontal pulling exercise for the latissimus dorsi and upper back.", "Barbell Bent-Over Row", "Back" },
                    { "cable-chest-fly", "Resistance", "Cable", "Horizontal adduction isolation movement for the chest.", "Cable Chest Fly", "Chest" },
                    { "cable-woodchopper", "Resistance", "Cable", "Rotational resistance exercise for the obliques and trunk.", "Cable Woodchopper", "Core" },
                    { "conventional-deadlift", "Resistance", "Barbell", "Full posterior-chain pull from the floor.", "Conventional Deadlift", "Back" },
                    { "dumbbell-bicep-curl", "Resistance", "Dumbbell", "Elbow flexion exercise targeting the biceps brachii.", "Dumbbell Bicep Curl", "Arms" },
                    { "dumbbell-lateral-raise", "Resistance", "Dumbbell", "Shoulder abduction isolation exercise for the medial deltoid.", "Dumbbell Lateral Raise", "Shoulders" },
                    { "face-pull", "Resistance", "Cable", "Upper-back and rear-deltoid pull for shoulder health and posture.", "Rope Face Pull", "Shoulders" },
                    { "hanging-leg-raise", "Bodyweight", "Bodyweight", "Bodyweight hip and trunk flexion exercise for the anterior core.", "Hanging Leg Raise", "Core" },
                    { "incline-dumbbell-press", "Resistance", "Dumbbell", "Incline pressing exercise emphasizing the clavicular head of the pectorals.", "Incline Dumbbell Press", "Chest" },
                    { "lat-pulldown", "Resistance", "Cable", "Vertical pulling movement targeting the latissimus dorsi.", "Wide-Grip Lat Pulldown", "Back" },
                    { "leg-press", "Resistance", "Machine", "Machine-based compound knee and hip extension exercise.", "Leg Press", "Legs" },
                    { "overhead-barbell-press", "Resistance", "Barbell", "Standing vertical press for anterior and medial deltoids.", "Overhead Barbell Press", "Shoulders" },
                    { "parallel-bar-dip", "Bodyweight", "Bodyweight", "Bodyweight pressing movement targeting the triceps and lower chest.", "Parallel Bar Dip", "Arms" },
                    { "pull-up", "Bodyweight", "Bodyweight", "Bodyweight vertical pull targeting the lats and upper back.", "Pull-Up", "Back" },
                    { "push-up", "Bodyweight", "Bodyweight", "Bodyweight horizontal press for chest, shoulders, and trunk stability.", "Push-Up", "Chest" },
                    { "romanian-deadlift", "Resistance", "Barbell", "Hip-hinge movement targeting the hamstrings and glutes.", "Romanian Deadlift", "Legs" },
                    { "tricep-rope-pushdown", "Resistance", "Cable", "Cable elbow extension movement targeting the triceps.", "Tricep Rope Pushdown", "Arms" },
                    { "walking-lunge", "Resistance", "Dumbbell", "Unilateral lower-body stride exercise for quadriceps and glute stability.", "Dumbbell Walking Lunge", "Legs" }
                });

            migrationBuilder.InsertData(
                table: "program_templates",
                columns: new[] { "id", "description", "name", "recommended_experience_level" },
                values: new object[,]
                {
                    { "full-body-foundation", "Balanced three-day full-body template covering major compound movements.", "Full Body Foundation", "Beginner" },
                    { "push-pull-legs-hypertrophy", "Six-day split grouping pressing, pulling, and lower-body volume.", "Push / Pull / Legs Hypertrophy", "Advanced" },
                    { "upper-lower-strength", "Four-day upper and lower body strength progression program.", "Upper / Lower Strength Split", "Intermediate" }
                });

            migrationBuilder.InsertData(
                table: "program_template_exercises",
                columns: new[] { "id", "display_order", "exercise_id", "rest_seconds", "target_load_kg", "target_reps", "target_sets", "template_id" },
                values: new object[,]
                {
                    { 1, 1, "barbell-back-squat", 120, 40m, 8, 3, "full-body-foundation" },
                    { 2, 2, "barbell-bench-press", 120, 35m, 8, 3, "full-body-foundation" },
                    { 3, 3, "barbell-bent-over-row", 90, 30m, 10, 3, "full-body-foundation" },
                    { 4, 4, "hanging-leg-raise", 60, null, 12, 3, "full-body-foundation" },
                    { 5, 1, "barbell-bench-press", 150, 60m, 6, 4, "upper-lower-strength" },
                    { 6, 2, "overhead-barbell-press", 120, 40m, 8, 3, "upper-lower-strength" },
                    { 7, 3, "barbell-back-squat", 150, 80m, 6, 4, "upper-lower-strength" },
                    { 8, 4, "romanian-deadlift", 120, 70m, 8, 3, "upper-lower-strength" },
                    { 9, 1, "incline-dumbbell-press", 90, 24m, 10, 4, "push-pull-legs-hypertrophy" },
                    { 10, 2, "lat-pulldown", 90, 55m, 10, 4, "push-pull-legs-hypertrophy" },
                    { 11, 3, "leg-press", 120, 120m, 12, 4, "push-pull-legs-hypertrophy" },
                    { 12, 4, "dumbbell-lateral-raise", 60, 10m, 15, 3, "push-pull-legs-hypertrophy" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercises_primary_muscle_group",
                table: "exercises",
                column: "primary_muscle_group");

            migrationBuilder.CreateIndex(
                name: "IX_program_template_exercises_exercise_id",
                table: "program_template_exercises",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_program_template_exercises_template_id_display_order",
                table: "program_template_exercises",
                columns: new[] { "template_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_version_exercises_exercise_id",
                table: "program_version_exercises",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_program_version_exercises_program_version_id_display_order",
                table: "program_version_exercises",
                columns: new[] { "program_version_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_versions_program_id_version_number",
                table: "program_versions",
                columns: new[] { "program_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_programs_owner_uid",
                table: "training_programs",
                column: "owner_uid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_template_exercises");

            migrationBuilder.DropTable(
                name: "program_version_exercises");

            migrationBuilder.DropTable(
                name: "program_templates");

            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "program_versions");

            migrationBuilder.DropTable(
                name: "training_programs");

            migrationBuilder.DropColumn(
                name: "age",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "auth_provider",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "available_equipment",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "display_timezone",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "email",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "experience_level",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "height_cm",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "onboarding_completed",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "primary_goal",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                table: "user_accounts");
        }
    }
}
