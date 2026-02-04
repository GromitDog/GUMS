using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BadgeClauseId",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UmaDefinitionId",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityCompletions_Activities_MeetingActivityId",
                        column: x => x.MeetingActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwardTrackings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GoldChallengeComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    GoldChallengeDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwardTrackings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BadgeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Theme = table.Column<int>(type: "INTEGER", nullable: false),
                    BadgeType = table.Column<int>(type: "INTEGER", nullable: false),
                    Section = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: true),
                    SkillsBuilderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RequiredCompletions = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UmaDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Theme = table.Column<int>(type: "INTEGER", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UmaDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BadgeClauses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BadgeDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeClauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeClauses_BadgeDefinitions_BadgeDefinitionId",
                        column: x => x.BadgeDefinitionId,
                        principalTable: "BadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_BadgeClauseId",
                table: "Activities",
                column: "BadgeClauseId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UmaDefinitionId",
                table: "Activities",
                column: "UmaDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCompletions_MeetingActivityId",
                table: "ActivityCompletions",
                column: "MeetingActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCompletions_MeetingActivityId_MembershipNumber",
                table: "ActivityCompletions",
                columns: new[] { "MeetingActivityId", "MembershipNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCompletions_MembershipNumber",
                table: "ActivityCompletions",
                column: "MembershipNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AwardTrackings_MembershipNumber",
                table: "AwardTrackings",
                column: "MembershipNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BadgeClauses_BadgeDefinitionId",
                table: "BadgeClauses",
                column: "BadgeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeDefinitions_BadgeType",
                table: "BadgeDefinitions",
                column: "BadgeType");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeDefinitions_Section",
                table: "BadgeDefinitions",
                column: "Section");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeDefinitions_Theme",
                table: "BadgeDefinitions",
                column: "Theme");

            migrationBuilder.CreateIndex(
                name: "IX_UmaDefinitions_Theme",
                table: "UmaDefinitions",
                column: "Theme");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_BadgeClauses_BadgeClauseId",
                table: "Activities",
                column: "BadgeClauseId",
                principalTable: "BadgeClauses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_UmaDefinitions_UmaDefinitionId",
                table: "Activities",
                column: "UmaDefinitionId",
                principalTable: "UmaDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_BadgeClauses_BadgeClauseId",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_Activities_UmaDefinitions_UmaDefinitionId",
                table: "Activities");

            migrationBuilder.DropTable(
                name: "ActivityCompletions");

            migrationBuilder.DropTable(
                name: "AwardTrackings");

            migrationBuilder.DropTable(
                name: "BadgeClauses");

            migrationBuilder.DropTable(
                name: "UmaDefinitions");

            migrationBuilder.DropTable(
                name: "BadgeDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Activities_BadgeClauseId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_UmaDefinitionId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "BadgeClauseId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "UmaDefinitionId",
                table: "Activities");
        }
    }
}
