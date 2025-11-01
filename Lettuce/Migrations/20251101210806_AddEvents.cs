using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lettuce.Migrations
{
    /// <inheritdoc />
    public partial class AddEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionByPawnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionToPawnId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventText = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    NewX = table.Column<int>(type: "integer", nullable: false),
                    NewY = table.Column<int>(type: "integer", nullable: false),
                    OldX = table.Column<int>(type: "integer", nullable: false),
                    OldY = table.Column<int>(type: "integer", nullable: false),
                    LettuceCount = table.Column<int>(type: "integer", nullable: false),
                    Died = table.Column<bool>(type: "boolean", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    ActionById = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionToId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Pawns_ActionById",
                        column: x => x.ActionById,
                        principalTable: "Pawns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Pawns_ActionToId",
                        column: x => x.ActionToId,
                        principalTable: "Pawns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_ActionById",
                table: "Events",
                column: "ActionById");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ActionToId",
                table: "Events",
                column: "ActionToId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
