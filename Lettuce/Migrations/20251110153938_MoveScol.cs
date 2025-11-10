using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lettuce.Migrations
{
    /// <inheritdoc />
    public partial class MoveScol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Pawns",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                columns: new[] { "X", "Y" },
                values: new object[] { -8, 11 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Pawns",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                columns: new[] { "X", "Y" },
                values: new object[] { -5, 5 });
        }
    }
}
