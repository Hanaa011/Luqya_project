using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Reports
{
    public class EfCoreReportRepository :
        EfCoreRepository<LostFoundDbContext, Report, Guid>,
        IReportRepository
    {
        public EfCoreReportRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<List<Report>> GetListByCategoryAsync(Guid categoryId)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet.Where(r => r.CategoryId == categoryId).ToListAsync();
        }

        public async Task<List<Report>> GetMatchCandidatesAsync(Guid reportId, ReportType oppositeType)
        {
            var dbSet = await GetDbSetAsync();

            return await dbSet
                .Where(r => r.Id != reportId)
                .Where(r => r.Type == oppositeType)
                .Where(r => r.Status == ReportStatus.Open)
                .Where(r => r.EmbeddingJson != null)
                .ToListAsync();
        }

        public async Task<List<Report>> GetSearchableReportsAsync(ReportType? type)
        {
            var dbSet = await GetDbSetAsync();

            var queryable = dbSet.Where(r => r.EmbeddingJson != null);

            if (type.HasValue)
            {
                queryable = queryable.Where(r => r.Type == type.Value);
            }

            return await queryable.ToListAsync();
        }

        public async Task<List<Report>> GetReportsByTypeAsync(ReportType? type)
        {
            var dbSet = await GetDbSetAsync();

            var queryable = type.HasValue ? dbSet.Where(r => r.Type == type.Value) : dbSet;

            return await queryable.ToListAsync();
        }
    }
}
