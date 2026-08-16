using System;

namespace LostFound.AI.Caching
{
    // The "Concept Cache" layer Phase 2A Part 5 requires - a read-through
    // cache in front of LostFound.AI.Concepts.IConceptRepository.GetByIdAsync,
    // which (Parts 3/4) hits SQLite on every single lookup with no caching
    // at all. Separate from IEmbeddingCache (Part 2, caches float[] vectors)
    // since concepts and embeddings have different sizes, invalidation
    // triggers, and lifetimes.
    public interface IConceptCache
    {
        int Count { get; }

        bool TryGet(Guid conceptId, out Concepts.Concept? concept);

        void Set(Guid conceptId, Concepts.Concept concept);

        // Called after IConceptRepository.UpsertAsync/RollbackAsync/SoftDeleteAsync
        // so a cached copy is never served after the underlying data changes.
        void Invalidate(Guid conceptId);
    }
}
