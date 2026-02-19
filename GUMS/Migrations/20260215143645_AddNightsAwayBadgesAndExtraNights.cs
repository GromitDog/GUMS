using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddNightsAwayBadgesAndExtraNights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExtraNightsAway",
                table: "Persons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NightsAwayBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Milestone = table.Column<int>(type: "INTEGER", nullable: false),
                    DateAwarded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NightsAwayBadges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NightsAwayBadges_MembershipNumber_Milestone",
                table: "NightsAwayBadges",
                columns: new[] { "MembershipNumber", "Milestone" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NightsAwayBadges");

            migrationBuilder.DropColumn(
                name: "ExtraNightsAway",
                table: "Persons");
        }
    }
}
