using System;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Ontology
{
    // Same/RelatedCluster/UnrelatedCluster mirror the tiers the legacy
    // static ObjectTypeRelationship table already distinguishes (Phone-vs-
    // Wallet vs Phone-vs-Car aren't equally "wrong"). Unknown is a fourth,
    // honest state the static table never had: "the ontology has no data
    // to judge this pair" - returned instead of guessing when either side
    // fails to resolve to a Concept.
    public enum ObjectTypeCompatibility
    {
        Unknown,
        Same,
        RelatedCluster,
        UnrelatedCluster
    }

    // Phase 2B Part 4's ontology-driven replacement for the legacy static
    // ObjectTypeRelationship lookup table - "Replace static mappings with
    // ontology-driven relationships. Use the Knowledge Graph as the primary
    // source" (spec). Consumed only by the new hybrid pipeline path
    // (SemanticSearchOrchestrator); ObjectTypeRelationship itself is left
    // untouched since the legacy deterministic scoring path still depends
    // on it.
    public interface IObjectTypeCompatibilityService
    {
        Task<ObjectTypeCompatibility> ClassifyAsync(
            Guid? queryConceptId,
            string? candidateObjectType,
            string languageCode,
            CancellationToken cancellationToken = default);
    }
}
