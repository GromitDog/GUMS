using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardedThemeAwardsAndLevelDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BronzeAwardedDate",
                table: "AwardTrackings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GoldAwardedDate",
                table: "AwardTrackings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilverAwardedDate",
                table: "AwardTrackings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AwardedThemeAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Theme = table.Column<int>(type: "INTEGER", nullable: false),
                    DateAwarded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwardedThemeAwards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwardedThemeAwards_MembershipNumber_Theme",
                table: "AwardedThemeAwards",
                columns: new[] { "MembershipNumber", "Theme" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AwardedThemeAwards");

            migrationBuilder.DropColumn(
                name: "BronzeAwardedDate",
                table: "AwardTrackings");

            migrationBuilder.DropColumn(
                name: "GoldAwardedDate",
                table: "AwardTrackings");

            migrationBuilder.DropColumn(
                name: "SilverAwardedDate",
                table: "AwardTrackings");
        }
    }
}
