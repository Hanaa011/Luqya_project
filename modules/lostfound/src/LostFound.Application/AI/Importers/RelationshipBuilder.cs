using System.Collections.Generic;
using LostFound.AI.Graph;

namespace LostFound.AI.Importers
{
    internal sealed class RelationshipBuilder : IRelationshipBuilder
    {
        public IReadOnlyList<ConceptRelationship> BuildRelationships(
            IReadOnlyList<RawRelationshipRecord> relationships,
            IReadOnlyDictionary<string, Concepts.Concept> conceptsByCanonicalName)
        {
            var result = new List<ConceptRelationship>();

            // A single logical edge is commonly asserted more than once in
            // the raw data - e.g. every per-language record of a concept
            // carries its own RawConceptRecord.ParentNames claim, all of
            // which resolve to the same (source, target) concept pair once
            // canonicalized. Deduplicating on the RESOLVED ids (not the raw
            // names, which legitimately differ per language) keeps exactly
            // one persisted row per real-world relationship.
            var seenEdges = new HashSet<(System.Guid Source, System.Guid Target, RelationshipType Type)>();

            foreach (var relationship in relationships)
            {
                if (!conceptsByCanonicalName.TryGetValue(relationship.SourceConceptName, out var source))
                {
                    continue; // broken reference - already flagged by IDataValidator, silently dropped here
                }

                if (!conceptsByCanonicalName.TryGetValue(relationship.TargetConceptName, out var target))
                {
                    continue;
                }

                if (source.Id == target.Id)
                {
                    continue; // circular reference guard - source/target merged into the same concept
                }

                if (!seenEdges.Add((source.Id, target.Id, relationship.RelationshipType)))
                {
                    continue; // same logical edge already produced from a different raw record
                }

                result.Add(new ConceptRelationship
                {
                    Id = DeterministicGuid.From($"{relationship.SourceDataset}:{source.Id}:{relationship.RelationshipType}:{target.Id}"),
                    SourceConceptId = source.Id,
                    TargetConceptId = target.Id,
                    RelationshipType = relationship.RelationshipType,
                    Weight = relationship.Weight,
                    SourceDataset = relationship.SourceDataset
                });
            }

            return result;
        }
    }
}
