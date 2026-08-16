using System;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Concepts
{
    // Fast, in-memory alias/synonym/dialect/misspelling -> ConceptId lookup,
    // per Phase 1's storage decision ("a memory-mapped/serialized in-memory
    // index built from it [SQLite] at startup for hot-path lookups"). The
    // index must be rebuilt after bulk writes (e.g. Phase 2A Part 4's
    // dataset importers) - see RebuildIndexAsync.
    public interface IAliasResolver
    {
        // text must already be normalized (see IConceptNormalizer) -
        // resolvers never normalize internally, so callers control exactly
        // which normalization rules were applied.
        Task<Guid?> ResolveAsync(string normalizedText, string languageCode, CancellationToken cancellationToken = default);

        Task RebuildIndexAsync(CancellationToken cancellationToken = default);
    }
}
