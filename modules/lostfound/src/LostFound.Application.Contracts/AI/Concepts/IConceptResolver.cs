using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Concepts
{
    // Entry point from raw free text to a resolved Concept: normalize (via
    // IConceptNormalizer) then look up (via IAliasResolver + IConceptRepository).
    // This is the "IConceptResolver.Resolve" component named throughout the
    // Phase 1 architecture docs as one of the local-first priority layers.
    // Deliberately does not do retrieval/ranking/expansion across multiple
    // concepts - that's Phase 2B's query pipeline, built on top of this.
    public interface IConceptResolver
    {
        Task<Concept?> ResolveAsync(string text, string languageCode, CancellationToken cancellationToken = default);
    }
}
