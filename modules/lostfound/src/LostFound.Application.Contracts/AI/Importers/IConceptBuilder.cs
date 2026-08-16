using System.Collections.Generic;

namespace LostFound.AI.Importers
{
    // ConceptsByRawName maps EVERY raw record's original CanonicalName
    // (case-insensitive) - not just the merged concept's own chosen
    // CanonicalName - to the resulting merged Concept, so
    // IRelationshipBuilder can resolve a relationship that names any
    // pre-merge variant (e.g. a relationship referencing the Arabic label
    // of a concept whose merged/primary CanonicalName ended up English).
    public sealed record ConceptBuildResult(
        IReadOnlyList<Concepts.Concept> Concepts,
        IReadOnlyDictionary<string, Concepts.Concept> ConceptsByRawName);

    // Turns a full batch of raw records into the final set of canonical
    // Concepts, composing IDeduplicationService (group) + ICanonicalizer
    // (merge each group). The single entry point IImportCoordinator calls
    // for the "Deduplication -> Conflict Resolution -> Canonical Concept
    // Builder" stages of the pipeline.
    public interface IConceptBuilder
    {
        ConceptBuildResult BuildConcepts(IReadOnlyList<RawConceptRecord> validatedRecords);
    }
}
