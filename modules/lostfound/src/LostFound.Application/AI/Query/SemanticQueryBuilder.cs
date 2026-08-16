using System;
using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Query
{
    internal sealed class SemanticQueryBuilder : ISemanticQueryBuilder
    {
        public SemanticQuery Build(
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
            QueryDiagnostics diagnostics)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var finalWords = new List<string>();

            foreach (var word in tokens.Concat(expandedTerms.Select(t => t.Term)))
            {
                if (!string.IsNullOrWhiteSpace(word) && seen.Add(word))
                {
                    finalWords.Add(word);
                }
            }

            return new SemanticQuery(
                rawText,
                languageCode,
                normalizedText,
                correctedText,
                tokens,
                lemmas,
                intent,
                entities,
                resolvedConceptId,
                expandedTerms,
                string.Join(' ', finalWords),
                diagnostics);
        }
    }
}
