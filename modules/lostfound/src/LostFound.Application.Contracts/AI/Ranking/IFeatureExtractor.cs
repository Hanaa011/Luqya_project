using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;
using LostFound.AI.Retrieval;

namespace LostFound.AI.Ranking
{
    // Computes RankingFeatures for one candidate from real data: Phase 2B
    // Part 2's own per-strategy contributions (Vector/BM25/Graph/Exact/
    // Category/Brand/Color/Material/Location/Time all map directly) plus a
    // few features Part 2 didn't compute on its own (ObjectTypeSimilarity,
    // AliasMatch) derived fresh from the report and query here.
    public interface IFeatureExtractor
    {
        Task<RankingFeatures> ExtractAsync(
            FusedCandidate candidate, SearchableReport report, SemanticQuery query, CancellationToken cancellationToken = default);
    }
}
