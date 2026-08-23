using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Matches;
using LostFound.Reports;

namespace LostFound.EntityFrameworkCore
{
    public static class ReportClaimConfiguration
    {
        public static void ConfigureReportClaim(this ModelBuilder builder)
        {
            builder.Entity<ReportClaim>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "ReportClaims", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();

                b.Property(x => x.ObservedScorePercentage).HasColumnType("decimal(5,2)");

                b.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict).IsRequired();

                // Not unique: ExistsAsync is used to prevent duplicates at
                // the application layer (idempotent create), but nothing
                // in this table's own shape depends on that always holding
                // - a plain lookup index is enough for the two real access
                // patterns (per-report, per-claimant).
                b.HasIndex(x => new { x.ReportId, x.ClaimantUserId });
            });
        }
    }
}
