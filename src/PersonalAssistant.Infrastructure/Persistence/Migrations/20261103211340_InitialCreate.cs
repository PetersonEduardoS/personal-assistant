using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    WhatsAppMessageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_PhoneNumber_Id",
                table: "ConversationMessages",
                columns: new[] { "PhoneNumber", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_WhatsAppMessageId",
                table: "ConversationMessages",
                column: "WhatsAppMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMessages");
        }
    }
}
