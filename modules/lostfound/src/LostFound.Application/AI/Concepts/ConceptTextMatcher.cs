using System;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Concepts
{
    // Shared by structured-attribute retrieval (AI.Retrieval.AttributeRetrievers)
    // and ranking's metadata-mismatch penalties (AI.Ranking.RankingEngine):
    // both need to decide whether two pieces of text - a query's recognized
    // entity value and a report's stored field - refer to the SAME real-world
    // concept, not just the same literal string. "Canon" / "كانون" / "Canon
    // Inc." must all be treated as equal without hardcoding any of those
    // specific spellings anywhere. This resolves both sides through the same
    // ontology-backed IConceptResolver query understanding already uses
    // (alias/synonym/cross-language aware), and falls back to literal
    // case-insensitive text equality only when concept resolution can't
    // place one side (e.g. a value the ontology doesn't know yet) - concept
    // matching only ADDS coverage on top of the pre-existing literal
    // guarantee, it never removes it.
    internal static class ConceptTextMatcher
    {
        public static async Task<bool> AreSameAsync(
            string left, string right, string primaryLanguage, IConceptResolver conceptResolver, CancellationToken cancellationToken)
        {
            if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var leftConceptId = await ResolveConceptIdAsync(left, primaryLanguage, conceptResolver, cancellationToken);
            if (!leftConceptId.HasValue)
            {
                return false;
            }

            var rightConceptId = await ResolveConceptIdAsync(right, primaryLanguage, conceptResolver, cancellationToken);
            return rightConceptId.HasValue && leftConceptId.Value == rightConceptId.Value;
        }

        // Classification output (Gemini/OpenAI/LocalClassificationProvider)
        // is consistently English-labeled regardless of the report's own
        // description language (e.g. "Canon"/"Black"/"Camera"), while a
        // query's recognized entity is in whatever language the user typed
        // - so a stored field very often needs the "en" fallback even when
        // the query itself is Arabic/Urdu.
        private static async Task<Guid?> ResolveConceptIdAsync(
            string text, string primaryLanguage, IConceptResolver conceptResolver, CancellationToken cancellationToken)
        {
            var concept = await conceptResolver.ResolveAsync(text, primaryLanguage, cancellationToken);
            if (concept != null)
            {
                return concept.Id;
            }

            if (!string.Equals(primaryLanguage, "en", StringComparison.OrdinalIgnoreCase))
            {
                concept = await conceptResolver.ResolveAsync(text, "en", cancellationToken);
            }

            return concept?.Id;
        }
    }
}
