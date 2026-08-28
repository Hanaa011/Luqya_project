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

        // Used by AiSearchAppService.EnrichAsync to look up ai_service's own
        // match results. Deliberately NOT restricted to EmbeddingJson != null
        // like GetSearchableReportsAsync - that requirement is specific to
        // the legacy AiMatchingService/HybridSearchEngine consumer, which
        // scores from a precomputed embedding. ai_service computes its own
        // embeddings live and reads its candidate pool from every report of
        // the given type regardless of EmbeddingJson, so this mirrors that
        // same pool to avoid silently dropping a real match during
        // enrichment just because the background embedding job hasn't run.
        Task<List<Report>> GetReportsByTypeAsync(ReportType? type);

        // Used by AiSearchAppService.EnrichAsync to look up exactly the
        // reports named by ai_service's own match results - a small, known
        // id set, not the whole table (see EnrichAsync's remarks for why
        // that distinction matters for latency).
        Task<List<Report>> GetByIdsAsync(IEnumerable<Guid> ids);
    }
}
