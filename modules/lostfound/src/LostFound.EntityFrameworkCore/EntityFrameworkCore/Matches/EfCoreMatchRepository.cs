using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Matches
{
    public class EfCoreMatchRepository :
        EfCoreRepository<LostFoundDbContext, Match, Guid>,
        IMatchRepository
    {
        public EfCoreMatchRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<bool> ExistsForPairAsync(Guid lostReportId, Guid foundReportId)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.AnyAsync(m => m.LostReportId == lostReportId && m.FoundReportId == foundReportId);
        }

        public async Task<Match?> FindByPairAsync(Guid lostReportId, Guid foundReportId)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.FirstOrDefaultAsync(m => m.LostReportId == lostReportId && m.FoundReportId == foundReportId);
        }
    }
}
