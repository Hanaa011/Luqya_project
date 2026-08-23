using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Matches
{
    public class EfCoreReportClaimRepository :
        EfCoreRepository<LostFoundDbContext, ReportClaim, Guid>,
        IReportClaimRepository
    {
        public EfCoreReportClaimRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<ReportClaim?> FindAsync(Guid reportId, Guid claimantUserId)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.FirstOrDefaultAsync(c => c.ReportId == reportId && c.ClaimantUserId == claimantUserId);
        }
    }
}
