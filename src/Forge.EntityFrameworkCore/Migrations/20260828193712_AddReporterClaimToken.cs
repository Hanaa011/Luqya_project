using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forge.Migrations
{
    /// <inheritdoc />
    public partial class AddReporterClaimToken : Migration
    {
        /// <inheritdoc />
        // Scoped down by hand from the raw `dotnet ef migrations add` output:
        // this repo's migration history never recorded a migration for
        // LostFoundConversations/LostFoundConversationMessages (they exist
        // in the model/snapshot but were evidently created out-of-band
        // against the shared dev database at some point) - confirmed via a
        // read-only sys.tables check against that server before this file
        // was edited. Re-generating the diff against a snapshot that
        // predates those tables made this migration try to CREATE them
        // again, which would fail wherever they already exist. This
        // migration is scoped to the one genuinely new table -
        // LostFoundReporterClaimTokens - only.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LostFoundReporterClaimTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_LostFoundReporterClaimTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundReporterClaimTokens_LostFoundReporters_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "LostFoundReporters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReporterClaimTokens_ReporterId",
                table: "LostFoundReporterClaimTokens",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReporterClaimTokens_TokenHash",
                table: "LostFoundReporterClaimTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LostFoundReporterClaimTokens");
        }
    }
}
