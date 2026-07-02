using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPatrols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PatrolId",
                table: "Persons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatrolRole",
                table: "Persons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Patrols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Section = table.Column<int>(type: "INTEGER", nullable: false),
                    EmblemBadgeDefinitionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patrols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patrols_BadgeDefinitions_EmblemBadgeDefinitionId",
                        column: x => x.EmblemBadgeDefinitionId,
                        principalTable: "BadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Persons_PatrolId",
                table: "Persons",
                column: "PatrolId");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_PatrolId_PatrolRole",
                table: "Persons",
                columns: new[] { "PatrolId", "PatrolRole" },
                unique: true,
                filter: "\"PatrolRole\" <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Person_PatrolRole_Requires_Patrol",
                table: "Persons",
                sql: "\"PatrolRole\" = 0 OR \"PatrolId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Patrols_EmblemBadgeDefinitionId",
                table: "Patrols",
                column: "EmblemBadgeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Patrols_Section_Name",
                table: "Patrols",
                columns: new[] { "Section", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Persons_Patrols_PatrolId",
                table: "Persons",
                column: "PatrolId",
                principalTable: "Patrols",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Persons_Patrols_PatrolId",
                table: "Persons");

            migrationBuilder.DropTable(
                name: "Patrols");

            migrationBuilder.DropIndex(
                name: "IX_Persons_PatrolId",
                table: "Persons");

            migrationBuilder.DropIndex(
                name: "IX_Persons_PatrolId_PatrolRole",
                table: "Persons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Person_PatrolRole_Requires_Patrol",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "PatrolId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "PatrolRole",
                table: "Persons");
        }
    }
}
