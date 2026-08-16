using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Query
{
    // Cache key must include normalized query + language + model version +
    // knowledge version (spec) - a cached SemanticQuery from before the
    // active embedding model or knowledge graph changed could silently
    // reference stale ConceptIds/expansion terms.
    public interface IQueryCache
    {
        bool TryGet(string cacheKey, out SemanticQuery? query);

        void Set(string cacheKey, SemanticQuery query);

        Task<string> BuildCacheKeyAsync(string normalizedText, string languageCode, CancellationToken cancellationToken = default);
    }
}
