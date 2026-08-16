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

                b.HasOne<Reporter>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict).IsRequired();
                b.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict).IsRequired();

                b.HasIndex(x => x.ReporterId);
            });
        }
    }
}
