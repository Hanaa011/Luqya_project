using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Categories;

namespace LostFound.EntityFrameworkCore
{
    public static class CategoryConfiguration
    {
        public static void ConfigureCategory(this ModelBuilder builder)
        {
            builder.Entity<Category>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "Categories", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).IsRequired().HasMaxLength(CategoryConsts.MaxNameLength);
                b.Property(x => x.Icon).HasMaxLength(CategoryConsts.MaxIconLength);
                b.HasIndex(x => x.Name);
            });
        }
    }
}
