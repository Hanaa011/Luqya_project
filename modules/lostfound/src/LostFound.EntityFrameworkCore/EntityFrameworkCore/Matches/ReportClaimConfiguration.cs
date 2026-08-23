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

                // Phase 4 Part 8 (Task B): default true so any row from
                // before this column existed (all of which were "this is
                // my item" grants under Part 6's original, single-purpose
                // design) is correctly interpreted as IsMine=true by the
                // migration itself, not just by application-layer code.
                b.Property(x => x.IsMine).HasDefaultValue(true);

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
