using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Reporters;

namespace LostFound.EntityFrameworkCore
{
    public static class ReporterClaimTokenConfiguration
    {
        public static void ConfigureReporterClaimToken(this ModelBuilder builder)
        {
            builder.Entity<ReporterClaimToken>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "ReporterClaimTokens", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();

                b.Property(x => x.TokenHash)
                    .IsRequired()
                    .HasMaxLength(64);

                b.HasIndex(x => x.TokenHash).IsUnique();
                b.HasIndex(x => x.ReporterId);

                // Reporter lives in this same module/DbContext (unlike
                // IdentityUserId, which crosses into the Host module and
                // stays a plain column) - a real FK, matching Notification
                // and Report's own ReporterId configuration.
                b.HasOne<Reporter>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict).IsRequired();
            });
        }
    }
}
