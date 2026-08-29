using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forge.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationIdentityUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ReporterId",
                table: "LostFoundNotifications",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "IdentityUserId",
                table: "LostFoundNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundNotifications_IdentityUserId",
                table: "LostFoundNotifications",
                column: "IdentityUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LostFoundNotifications_IdentityUserId",
                table: "LostFoundNotifications");

            migrationBuilder.DropColumn(
                name: "IdentityUserId",
                table: "LostFoundNotifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReporterId",
                table: "LostFoundNotifications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
