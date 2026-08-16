using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Storage
{
    // Persistent (survives process restarts) embedding vector cache, keyed
    // by content hash AND model version - so upgrading the local model never
    // silently serves a stale vector computed by a previous model version.
    // Not to be confused with Report.EmbeddingJson/ImageEmbeddingJson (the
    // durable source of truth for a report's own embedding, per Phase 1
    // Part 6) - this store exists purely to avoid recomputation for
    // arbitrary text (most importantly, repeated search queries).
    public interface IEmbeddingStore
    {
        Task<float[]?> TryGetAsync(string cacheKey, string modelVersion, CancellationToken cancellationToken = default);

        Task SaveAsync(string cacheKey, string modelVersion, float[] embedding, CancellationToken cancellationToken = default);

        // Called when a model version is retired/rolled back, so stale
        // vectors from an old model never get served once it stops being
        // active.
        Task InvalidateModelVersionAsync(string modelVersion, CancellationToken cancellationToken = default);
    }
}
