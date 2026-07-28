using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LostFound.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LostFoundCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_LostFoundCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_LostFoundLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
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
                    table.PrimaryKey("PK_LostFoundProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundReporters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PreferredContact = table.Column<byte>(type: "tinyint", nullable: false),
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
                    table.PrimaryKey("PK_LostFoundReporters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationDetails = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LostFoundDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsItemWithFinder = table.Column<bool>(type: "bit", nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AiObjectType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AiBrand = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AiTagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAiClassified = table.Column<bool>(type: "bit", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageEmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_LostFoundReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundReports_LostFoundCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "LostFoundCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LostFoundReports_LostFoundLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "LostFoundLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundReports_LostFoundReporters_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "LostFoundReporters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LostReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimilarityScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MatchReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsAutoGenerated = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_LostFoundMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundMatches_LostFoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "LostFoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundMatches_LostFoundReports_LostReportId",
                        column: x => x.LostReportId,
                        principalTable: "LostFoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_LostFoundNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundNotifications_LostFoundReporters_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "LostFoundReporters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundNotifications_LostFoundReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "LostFoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundCategories_Name",
                table: "LostFoundCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundMatches_FoundReportId",
                table: "LostFoundMatches",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundMatches_LostReportId_FoundReportId",
                table: "LostFoundMatches",
                columns: new[] { "LostReportId", "FoundReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundMatches_Status",
                table: "LostFoundMatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundNotifications_ReporterId",
                table: "LostFoundNotifications",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundNotifications_ReportId",
                table: "LostFoundNotifications",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundProducts_Name",
                table: "LostFoundProducts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReporters_IdentityUserId",
                table: "LostFoundReporters",
                column: "IdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReporters_Phone",
                table: "LostFoundReporters",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReports_CategoryId",
                table: "LostFoundReports",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReports_LocationId",
                table: "LostFoundReports",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReports_ReporterId",
                table: "LostFoundReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReports_Status",
                table: "LostFoundReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundReports_Type",
                table: "LostFoundReports",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LostFoundMatches");

            migrationBuilder.DropTable(
                name: "LostFoundNotifications");

            migrationBuilder.DropTable(
                name: "LostFoundProducts");

            migrationBuilder.DropTable(
                name: "LostFoundReports");

            migrationBuilder.DropTable(
                name: "LostFoundCategories");

            migrationBuilder.DropTable(
                name: "LostFoundLocations");

            migrationBuilder.DropTable(
                name: "LostFoundReporters");
        }
    }
}
