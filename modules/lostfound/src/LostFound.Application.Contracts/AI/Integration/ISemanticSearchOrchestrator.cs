using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.Reports;

namespace LostFound.AI.Integration
{
    // Phase 2B Part 4's new live search path: IQueryPipeline -> IHybridSearchEngine
    // -> IRankingEngine (object-type/category/brand/color metadata penalties
    // applied here as of PHASE-VALIDATION-06, before results are sorted) ->
    // RankedReportResult, composing every subsystem built across Phase 2A/2B
    // into the same public shape AiMatchingService already returns. Text-only
    // by design (see IQueryPipeline.ProcessAsync) - AiMatchingService routes
    // image-based search to the legacy path regardless of the
    // HybridPipeline:Enabled flag.
    public interface ISemanticSearchOrchestrator
    {
        Task<List<RankedReportResult>> SearchAsync(
            string searchText, ReportType? type, int maxResults, CancellationToken cancellationToken = default);
    }
}
