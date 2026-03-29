using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeContactFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistrictCommissionerName",
                table: "UnitConfigurations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistrictCommissionerPhone",
                table: "UnitConfigurations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DivisionCommissionerName",
                table: "UnitConfigurations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DivisionCommissionerPhone",
                table: "UnitConfigurations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventAdditionalPeople",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EmergencyContactRelationship = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventAdditionalPeople", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventAdditionalPeople_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventContactOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ContactName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PrimaryPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SecondaryPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventContactOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventContactOverrides_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventHomeContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventHomeContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventHomeContacts_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventAdditionalPeople_MeetingId",
                table: "EventAdditionalPeople",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_EventContactOverrides_MeetingId",
                table: "EventContactOverrides",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_EventContactOverrides_MeetingId_MembershipNumber",
                table: "EventContactOverrides",
                columns: new[] { "MeetingId", "MembershipNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_EventHomeContacts_MeetingId",
                table: "EventHomeContacts",
                column: "MeetingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventAdditionalPeople");

            migrationBuilder.DropTable(
                name: "EventContactOverrides");

            migrationBuilder.DropTable(
                name: "EventHomeContacts");

            migrationBuilder.DropColumn(
                name: "DistrictCommissionerName",
                table: "UnitConfigurations");

            migrationBuilder.DropColumn(
                name: "DistrictCommissionerPhone",
                table: "UnitConfigurations");

            migrationBuilder.DropColumn(
                name: "DivisionCommissionerName",
                table: "UnitConfigurations");

            migrationBuilder.DropColumn(
                name: "DivisionCommissionerPhone",
                table: "UnitConfigurations");
        }
    }
}
