using System.Collections.Generic;
using LostFound.AI.Graph;

namespace LostFound.AI.Importers
{
    // Resolves RawRelationshipRecord's name-based source/target references
    // (and RawConceptRecord.ParentNames) against the just-built canonical
    // concepts, producing real ConceptRelationship rows with actual
    // ConceptIds. A name that doesn't resolve to any built concept is
    // dropped, not silently included with a Guid.Empty - see the spec's
    // "broken references" validation requirement.
    public interface IRelationshipBuilder
    {
        IReadOnlyList<ConceptRelationship> BuildRelationships(
            IReadOnlyList<RawRelationshipRecord> relationships,
            IReadOnlyDictionary<string, Concepts.Concept> conceptsByCanonicalName);
    }
}
