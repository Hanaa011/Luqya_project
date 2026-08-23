using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LostFound.Migrations
{
    /// <inheritdoc />
    public partial class AddReportClaimIsMine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMine",
                table: "LostFoundReportClaims",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMine",
                table: "LostFoundReportClaims");
        }
    }
}
