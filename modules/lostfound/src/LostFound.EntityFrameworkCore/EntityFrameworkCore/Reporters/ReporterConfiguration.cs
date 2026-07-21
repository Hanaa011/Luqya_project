using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Reporters;

namespace LostFound.EntityFrameworkCore
{
    public static class ReporterConfiguration
    {
        public static void ConfigureReporter(this ModelBuilder builder)
        {
            builder.Entity<Reporter>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "Reporters", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();

                b.Property(x => x.Name)
                    .HasMaxLength(ReporterConsts.MaxNameLength);

                b.Property(x => x.Phone)
                    .IsRequired()
                    .HasMaxLength(ReporterConsts.MaxPhoneLength);

                b.Property(x => x.Email)
                    .HasMaxLength(ReporterConsts.MaxEmailLength);

                // IdentityUserId is a PLAIN column, no FK/navigation -
                // IdentityUser lives in the Host module; modules should not
                // take hard EF dependencies on each other's tables.
                b.Property(x => x.IdentityUserId);

                b.HasIndex(x => x.IdentityUserId);
                b.HasIndex(x => x.Phone);
            });
        }
    }
}
