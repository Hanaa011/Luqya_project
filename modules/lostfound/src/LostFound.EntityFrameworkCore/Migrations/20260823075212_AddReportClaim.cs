using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LostFound.Migrations
{
    /// <inheritdoc />
    public partial class AddReportClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LostFoundReportClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObservedScorePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostFoundReportClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundReportClaims_LostFoundReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "LostFoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReportClaims_ReportId_ClaimantUserId",
                table: "LostFoundReportClaims",
                columns: new[] { "ReportId", "ClaimantUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LostFoundReportClaims");
        }
    }
}
