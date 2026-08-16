using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Importers
{
    internal sealed class ConceptBuilder(IDeduplicationService deduplicationService, ICanonicalizer canonicalizer) : IConceptBuilder
    {
        public ConceptBuildResult BuildConcepts(IReadOnlyList<RawConceptRecord> validatedRecords)
        {
            var groups = deduplicationService.GroupDuplicates(validatedRecords);
            var concepts = new List<Concepts.Concept>(groups.Count);
            var byRawName = new Dictionary<string, Concepts.Concept>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var concept = canonicalizer.Canonicalize(group);
                concepts.Add(concept);

                foreach (var record in group.Records)
                {
                    // First writer wins on a raw-name collision across
                    // otherwise-unrelated groups (rare, but possible with
                    // very short/ambiguous names) - deterministic and
                    // traceable rather than silently overwriting.
                    byRawName.TryAdd(record.CanonicalName, concept);
                }
            }

            return new ConceptBuildResult(concepts, byRawName);
        }
    }
}
