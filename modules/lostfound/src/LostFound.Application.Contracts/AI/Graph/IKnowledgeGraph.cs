using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Concepts;

namespace LostFound.AI.Graph
{
    // The graph abstraction the Part 3 spec requires: a facade over
    // IConceptRepository + IRelationshipRepository that exposes graph
    // TRAVERSAL (parents/children/related, bounded-depth expansion) rather
    // than making every caller compose the two repositories itself. Does
    // NOT implement retrieval/ranking - see the spec's "This phase does not
    // implement retrieval" - Phase 2B's hybrid retrieval engine is the
    // consumer of this interface, not a part of it.
    public interface IKnowledgeGraph
    {
        Task<Concept?> GetConceptAsync(Guid conceptId, CancellationToken cancellationToken = default);

        Task<Guid> AddConceptAsync(Concept concept, CancellationToken cancellationToken = default);

        Task AddRelationshipAsync(
            Guid sourceConceptId,
            Guid targetConceptId,
            RelationshipType relationshipType,
            double weight = 1.0,
            string? sourceDataset = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ConceptRelationship>> GetRelationshipsAsync(
            Guid conceptId, RelationshipType? relationshipType = null, CancellationToken cancellationToken = default);

        // Breadth-first traversal outward from conceptId along the given
        // relationship types (or all types if null), up to maxDepth hops -
        // the graph-structure counterpart to Phase 2B's later semantic/
        // embedding-based query expansion.
        Task<IReadOnlyList<Concept>> GetRelatedConceptsAsync(
            Guid conceptId,
            IReadOnlyCollection<RelationshipType>? relationshipTypes = null,
            int maxDepth = 1,
            CancellationToken cancellationToken = default);
    }
}
