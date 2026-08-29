using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Notifications;
using LostFound.Reporters;
using LostFound.Reports;

namespace LostFound.EntityFrameworkCore
{
    public static class NotificationConfiguration
    {
        public static void ConfigureNotification(this ModelBuilder builder)
        {
            builder.Entity<Notification>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "Notifications", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Title).HasMaxLength(NotificationConsts.MaxTitleLength);

                // ReporterId is now optional - an IdentityUserId-only row
                // (e.g. a missed-call notification) has no Reporter at all.
                b.HasOne<Reporter>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict).IsRequired();

                // IdentityUserId is a plain column (no cross-module FK) -
                // same convention as Reporter.IdentityUserId.
                b.Property(x => x.IdentityUserId);

                b.HasIndex(x => x.ReporterId);
                b.HasIndex(x => x.IdentityUserId);
            });
        }
    }
}
