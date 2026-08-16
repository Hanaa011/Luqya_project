using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Graph;

namespace LostFound.AI.Ontology
{
    // Real implementation, not a stub: resolves the candidate's object type
    // through the same IConceptResolver the query pipeline uses, then walks
    // IKnowledgeGraph's bounded-depth traversal to decide whether the query
    // concept and the candidate concept sit in the same cluster. Returns
    // Unknown - never a guess - whenever either side fails to resolve,
    // which is expected to be common against the small curated seed
    // ontology (Phase 2A Part 4); SemanticSearchOrchestrator treats Unknown
    // as "apply no adjustment", so an incomplete ontology degrades to
    // no-op, not to wrong penalties.
    internal sealed class ObjectTypeCompatibilityService(
        IConceptResolver conceptResolver,
        IKnowledgeGraph knowledgeGraph) : IObjectTypeCompatibilityService
    {
        private const int RelatedClusterMaxDepth = 2;

        public async Task<ObjectTypeCompatibility> ClassifyAsync(
            Guid? queryConceptId,
            string? candidateObjectType,
            string languageCode,
            CancellationToken cancellationToken = default)
        {
            if (queryConceptId is null || string.IsNullOrWhiteSpace(candidateObjectType))
            {
                return ObjectTypeCompatibility.Unknown;
            }

            var candidateConcept = await conceptResolver.ResolveAsync(candidateObjectType, languageCode, cancellationToken);
            if (candidateConcept is null)
            {
                return ObjectTypeCompatibility.Unknown;
            }

            if (candidateConcept.Id == queryConceptId.Value)
            {
                return ObjectTypeCompatibility.Same;
            }

            // IKnowledgeGraph.GetRelatedConceptsAsync only walks OUTWARD from
            // the given concept (source -> target edges - see its own
            // remarks). IsA edges are conventionally recorded child -> parent
            // (e.g. Smartphone IsA Phone), so a query for the parent
            // ("Phone") would never reach a child candidate ("Smartphone")
            // by traversing outward from the query concept alone. Checking
            // outward from the CANDIDATE concept too covers that direction,
            // using only the existing public facade (no direct
            // IRelationshipRepository access, keeping Part 3's abstraction
            // boundary intact).
            var relatedToQuery = await knowledgeGraph.GetRelatedConceptsAsync(
                queryConceptId.Value, relationshipTypes: null, maxDepth: RelatedClusterMaxDepth, cancellationToken: cancellationToken);

            if (relatedToQuery.Any(c => c.Id == candidateConcept.Id))
            {
                return ObjectTypeCompatibility.RelatedCluster;
            }

            var relatedToCandidate = await knowledgeGraph.GetRelatedConceptsAsync(
                candidateConcept.Id, relationshipTypes: null, maxDepth: RelatedClusterMaxDepth, cancellationToken: cancellationToken);

            return relatedToCandidate.Any(c => c.Id == queryConceptId.Value)
                ? ObjectTypeCompatibility.RelatedCluster
                : ObjectTypeCompatibility.UnrelatedCluster;
        }
    }
}
