using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Caching;

namespace LostFound.AI.Concepts
{
    // Read-through/write-through decorator adding IConceptCache in front of
    // any IConceptRepository - same decorator shape as Phase 2A Part 1's
    // ResilientProviderDecorator (wrap the real implementation, add one
    // cross-cutting concern, change nothing about its contract).
    internal sealed class CachedConceptRepository(IConceptRepository inner, IConceptCache cache) : IConceptRepository
    {
        public async Task<Concept?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (cache.TryGet(id, out var cached))
            {
                return cached;
            }

            var concept = await inner.GetByIdAsync(id, cancellationToken);
            if (concept != null)
            {
                cache.Set(id, concept);
            }

            return concept;
        }

        // Name/bulk lookups bypass the cache - it's keyed by Id only, per
        // IConceptCache's own contract (the hot path this exists for is
        // repeated by-Id lookups, e.g. IKnowledgeGraph traversal).
        public Task<Concept?> FindByCanonicalNameAsync(string canonicalName, CancellationToken cancellationToken = default) =>
            inner.FindByCanonicalNameAsync(canonicalName, cancellationToken);

        public Task<IReadOnlyList<Concept>> GetAllAsync(CancellationToken cancellationToken = default) =>
            inner.GetAllAsync(cancellationToken);

        public async Task UpsertAsync(Concept concept, CancellationToken cancellationToken = default)
        {
            await inner.UpsertAsync(concept, cancellationToken);
            cache.Set(concept.Id, concept); // write-through - avoids a guaranteed cache miss on the very next read
        }

        public Task<IReadOnlyList<Concept>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default) =>
            inner.GetHistoryAsync(id, cancellationToken);

        public async Task RollbackAsync(Guid id, int version, CancellationToken cancellationToken = default)
        {
            await inner.RollbackAsync(id, version, cancellationToken);
            cache.Invalidate(id);
        }

        public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await inner.SoftDeleteAsync(id, cancellationToken);
            cache.Invalidate(id);
        }
    }
}
