using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Locations;

namespace LostFound.EntityFrameworkCore
{
    public static class LocationConfiguration
    {
        public static void ConfigureLocation(this ModelBuilder builder)
        {
            builder.Entity<Location>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "Locations", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.PlaceName).IsRequired().HasMaxLength(LocationConsts.MaxPlaceNameLength);
            });
        }
    }
}
