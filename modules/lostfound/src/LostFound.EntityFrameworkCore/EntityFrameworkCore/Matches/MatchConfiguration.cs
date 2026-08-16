using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Matches;
using LostFound.Reports;

namespace LostFound.EntityFrameworkCore
{
    public static class MatchConfiguration
    {
        public static void ConfigureMatch(this ModelBuilder builder)
        {
            builder.Entity<Match>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "Matches", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();

                b.Property(x => x.SimilarityScore).HasColumnType("decimal(5,2)");
                b.Property(x => x.MatchReason).HasColumnType("nvarchar(max)");
                b.Property(x => x.Status).HasConversion<string>().HasMaxLength(MatchConsts.MaxStatusLength).IsRequired();

                b.HasOne<Report>().WithMany().HasForeignKey(x => x.LostReportId).OnDelete(DeleteBehavior.Restrict).IsRequired();
                b.HasOne<Report>().WithMany().HasForeignKey(x => x.FoundReportId).OnDelete(DeleteBehavior.Restrict).IsRequired();

                b.HasIndex(x => x.Status);
                b.HasIndex(x => new { x.LostReportId, x.FoundReportId }).IsUnique();
            });
        }
    }
}
