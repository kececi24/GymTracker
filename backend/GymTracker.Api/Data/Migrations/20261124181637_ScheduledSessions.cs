using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScheduledSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scheduled_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    start_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    display_timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cancelled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_scheduled_sessions_user_accounts_owner_uid",
                        column: x => x.owner_uid,
                        principalTable: "user_accounts",
                        principalColumn: "firebase_uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_sessions_owner_uid_start_at_utc",
                table: "scheduled_sessions",
                columns: new[] { "owner_uid", "start_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scheduled_sessions");
        }
    }
}
