using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Reporters
{
    public class EfCoreReporterRepository :
        EfCoreRepository<LostFoundDbContext, Reporter, Guid>,
        IReporterRepository
    {
        public EfCoreReporterRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<Reporter?> FindByIdentityUserIdAsync(Guid identityUserId)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.FirstOrDefaultAsync(r => r.IdentityUserId == identityUserId);
        }

        public async Task<Reporter?> FindByPhoneAsync(string phone)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.FirstOrDefaultAsync(r => r.Phone == phone);
        }
    }
}
