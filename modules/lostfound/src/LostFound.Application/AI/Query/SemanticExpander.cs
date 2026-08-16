using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Graph;

namespace LostFound.AI.Query
{
    internal sealed class SemanticExpander(IKnowledgeGraph knowledgeGraph, IConceptRepository conceptRepository) : ISemanticExpander
    {
        private static readonly RelationshipType[] ExpansionRelationshipTypes =
        {
            RelationshipType.IsA, RelationshipType.SimilarTo, RelationshipType.RelatedTo
        };

        public async Task<IReadOnlyList<ExpandedTerm>> ExpandAsync(
            Guid conceptId, string languageCode, CancellationToken cancellationToken = default)
        {
            var concept = await conceptRepository.GetByIdAsync(conceptId, cancellationToken);
            if (concept == null)
            {
                return Array.Empty<ExpandedTerm>();
            }

            var terms = new List<ExpandedTerm>();

            AddWords(terms, concept.Synonyms, languageCode, "Synonym", 0.9);
            AddWords(terms, concept.Aliases, languageCode, "Alias", 0.85);
            AddWords(terms, concept.DialectWords, languageCode, "DialectWord", 0.8);
            AddWords(terms, concept.CommonMisspellings, languageCode, "CommonMisspelling", 0.6);

            var related = await knowledgeGraph.GetRelatedConceptsAsync(
                conceptId, ExpansionRelationshipTypes, maxDepth: 1, cancellationToken);

            foreach (var relatedConcept in related)
            {
                var name = relatedConcept.LocalizedNames.TryGetValue(languageCode, out var localized)
                    ? localized
                    : relatedConcept.CanonicalName;

                terms.Add(new ExpandedTerm(name, "RelatedConcept", 0.5));
            }

            return terms;
        }

        private static void AddWords(
            List<ExpandedTerm> terms,
            IReadOnlyDictionary<string, IReadOnlyList<string>> wordsByLanguage,
            string languageCode,
            string reason,
            double weight)
        {
            if (!wordsByLanguage.TryGetValue(languageCode, out var words))
            {
                return;
            }

            foreach (var word in words)
            {
                terms.Add(new ExpandedTerm(word, reason, weight));
            }
        }
    }
}
