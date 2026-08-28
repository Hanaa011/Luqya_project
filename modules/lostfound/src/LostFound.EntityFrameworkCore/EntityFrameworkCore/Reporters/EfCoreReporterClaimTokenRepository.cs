using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Reporters
{
    public class EfCoreReporterClaimTokenRepository :
        EfCoreRepository<LostFoundDbContext, ReporterClaimToken, Guid>,
        IReporterClaimTokenRepository
    {
        public EfCoreReporterClaimTokenRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<ReporterClaimToken?> FindByTokenHashAsync(string tokenHash)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public async Task<ReporterClaimToken?> FindValidForReporterAsync(Guid reporterId, DateTime utcNow)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.FirstOrDefaultAsync(
                t => t.ReporterId == reporterId && t.UsedAt == null && t.ExpiresAt > utcNow);
        }
    }
}
