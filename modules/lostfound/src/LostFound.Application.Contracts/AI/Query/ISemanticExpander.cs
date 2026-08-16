using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Query
{
    // Expands a resolved concept into every term worth adding to the query
    // for retrieval: synonyms, aliases, dialect words, common misspellings
    // (all direct Concept fields - Phase 2A Part 3) and related concepts
    // (IsA/SimilarTo/RelatedTo neighbors via IKnowledgeGraph, Phase 2A Part 3).
    // This is the "Knowledge Graph Expansion" + "Synonym Expansion" pipeline
    // stages combined - they're the same operation (walking a Concept's own
    // data plus its graph neighbors), not two separate mechanisms.
    public interface ISemanticExpander
    {
        Task<IReadOnlyList<ExpandedTerm>> ExpandAsync(Guid conceptId, string languageCode, CancellationToken cancellationToken = default);
    }
}
