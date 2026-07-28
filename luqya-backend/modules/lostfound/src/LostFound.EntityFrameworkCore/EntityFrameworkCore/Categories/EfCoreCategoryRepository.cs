using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Categories
{
    public class EfCoreCategoryRepository :
        EfCoreRepository<LostFoundDbContext, Category, Guid>,
        ICategoryRepository
    {
        public EfCoreCategoryRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<Category?> FindByNameAsync(string name)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
        }
    }
}
