using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lettuce.Migrations
{
    /// <inheritdoc />
    public partial class FixKilledBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KilledBy",
                table: "Pawns",
                newName: "KilledById");

            migrationBuilder.CreateIndex(
                name: "IX_Pawns_KilledById",
                table: "Pawns",
                column: "KilledById");

            migrationBuilder.AddForeignKey(
                name: "FK_Pawns_Pawns_KilledById",
                table: "Pawns",
                column: "KilledById",
                principalTable: "Pawns",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pawns_Pawns_KilledById",
                table: "Pawns");

            migrationBuilder.DropIndex(
                name: "IX_Pawns_KilledById",
                table: "Pawns");

            migrationBuilder.RenameColumn(
                name: "KilledById",
                table: "Pawns",
                newName: "KilledBy");
        }
    }
}
