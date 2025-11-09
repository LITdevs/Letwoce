using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lettuce.Migrations
{
    /// <inheritdoc />
    public partial class Politics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Vote",
                table: "Pawns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScolVoteId",
                table: "Events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DropId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoteeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => new { x.Id, x.VoterId });
                    table.ForeignKey(
                        name: "FK_Votes_Events_DropId",
                        column: x => x.DropId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_Pawns_VoteeId",
                        column: x => x.VoteeId,
                        principalTable: "Pawns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_Pawns_VoterId",
                        column: x => x.VoterId,
                        principalTable: "Pawns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Pawns",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                column: "Vote",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_DropId",
                table: "Votes",
                column: "DropId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VoteeId",
                table: "Votes",
                column: "VoteeId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VoterId",
                table: "Votes",
                column: "VoterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropColumn(
                name: "Vote",
                table: "Pawns");

            migrationBuilder.DropColumn(
                name: "ScolVoteId",
                table: "Events");
        }
    }
}
