using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Reports
{
    public interface IReportRepository : IRepository<Report, Guid>
    {
        Task<List<Report>> GetListByCategoryAsync(Guid categoryId);

        // Category is NOT used as a filter/ranking factor for matching -
        // candidates are simply the opposite Type, still Open, with an
        // embedding already computed.
        Task<List<Report>> GetMatchCandidatesAsync(Guid reportId, ReportType oppositeType);

        // Used by AiSearchAppService - never regenerates embeddings, only
        // reads what is already stored.
        Task<List<Report>> GetSearchableReportsAsync(ReportType? type);
    }
}
