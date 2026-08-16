using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Graph
{
    public interface IRelationshipRepository
    {
        Task AddAsync(ConceptRelationship relationship, CancellationToken cancellationToken = default);

        // Both directions matter for graph traversal (e.g. "what is Phone a
        // parent of" vs "what is Smartphone a child of" are different
        // queries over the same edge) - see IKnowledgeGraph.GetRelatedConceptsAsync.
        Task<IReadOnlyList<ConceptRelationship>> GetBySourceAsync(
            Guid sourceConceptId, RelationshipType? relationshipType = null, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ConceptRelationship>> GetByTargetAsync(
            Guid targetConceptId, RelationshipType? relationshipType = null, CancellationToken cancellationToken = default);

        Task RemoveAsync(Guid relationshipId, CancellationToken cancellationToken = default);
    }
}
