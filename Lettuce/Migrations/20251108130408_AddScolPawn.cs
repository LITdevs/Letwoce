using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lettuce.Migrations
{
    /// <inheritdoc />
    public partial class AddScolPawn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Pawns",
                columns: new[] { "Id", "Actions", "AvatarUri", "Color", "DiscordId", "DisplayName", "Health", "KilledAt", "KilledBy", "X", "Y" },
                values: new object[] { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), 2147483647, "https://015-cdn.b-cdn.net/db49f42ed7b4a0f5a209dc00f8d780d5.png", 16777215, "1334788940082970654", "Supreme Court of Lettuce", 2147483647, null, null, -5, 5 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Pawns",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        }
    }
}
