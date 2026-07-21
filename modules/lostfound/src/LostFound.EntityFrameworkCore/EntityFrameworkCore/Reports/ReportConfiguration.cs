using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Reports;
using LostFound.Reporters;
using LostFound.Categories;
using LostFound.Locations;

namespace LostFound.EntityFrameworkCore
{
    public static class ReportConfiguration
    {
        public static void ConfigureReport(this ModelBuilder builder)
        {
            builder.Entity<Report>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "Reports", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();

                b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
                b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

                b.Property(x => x.Color).HasMaxLength(ReportConsts.MaxColorLength);
                b.Property(x => x.ImagePath).HasMaxLength(ReportConsts.MaxImagePathLength);
                b.Property(x => x.PickupLocation).HasMaxLength(ReportConsts.MaxPickupLocationLength);
                b.Property(x => x.LocationDetails).HasMaxLength(ReportConsts.MaxLocationDetailsLength);

                b.Property(x => x.AiObjectType).HasMaxLength(128);
                b.Property(x => x.AiBrand).HasMaxLength(128);
                b.Property(x => x.AiTagsJson).HasColumnType("nvarchar(max)");

                b.Property(x => x.EmbeddingJson).HasColumnType("nvarchar(max)");
                b.Property(x => x.ImageEmbeddingJson).HasColumnType("nvarchar(max)");

                b.HasOne<Reporter>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict).IsRequired();

                // CategoryId is OPTIONAL - AI fills it in later.
                b.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);

                // LocationId stays required - still the FK used for
                // analytics/filtering.
                b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict).IsRequired();

                b.HasIndex(x => x.Status);
                b.HasIndex(x => x.Type);
                b.HasIndex(x => x.CategoryId);
                b.HasIndex(x => x.LocationId);
            });
        }
    }
}
