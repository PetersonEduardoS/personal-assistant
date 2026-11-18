using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMorningBriefPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MorningBriefPreferences",
                columns: table => new
                {
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Time = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MorningBriefPreferences", x => x.PhoneNumber);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MorningBriefPreferences");
        }
    }
}
