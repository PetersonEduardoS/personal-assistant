using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShoppingItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Household = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Section = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AddedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BoughtAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_Household_BoughtAtUtc",
                table: "ShoppingItems",
                columns: new[] { "Household", "BoughtAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoppingItems");
        }
    }
}
