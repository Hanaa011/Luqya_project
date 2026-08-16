using System;
using System.Collections.Generic;

namespace LostFound.AI.Query
{
    // Pure, synchronous assembly step - takes the output of every earlier
    // pipeline stage (already computed) and produces the final SemanticQuery,
    // including FinalSemanticText (normalized text + deduplicated expansion
    // terms, joined - the string Phase 2B Part 2 will eventually hand to
    // IEmbeddingEngine). No I/O, so it's independently unit-testable from
    // the async stages that feed it.
    public interface ISemanticQueryBuilder
    {
        SemanticQuery Build(
            string rawText,
            string languageCode,
            string normalizedText,
            string correctedText,
            IReadOnlyList<string> tokens,
            IReadOnlyList<string> lemmas,
            QueryIntent intent,
            IReadOnlyList<RecognizedEntity> entities,
            Guid? resolvedConceptId,
            IReadOnlyList<ExpandedTerm> expandedTerms,
            QueryDiagnostics diagnostics);
    }
}
