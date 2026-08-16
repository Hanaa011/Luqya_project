using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Query;
using LostFound.AI.Retrieval;

namespace LostFound.AI.Ranking
{
    /// <remarks>
    /// PHASE-VALIDATION-07: ObjectTypeSimilarity/CategorySimilarity used to
    /// compare the query's recognized entity TEXT literally against the
    /// candidate's stored field - e.g. an Arabic query recognizing "شاحن"
    /// against a candidate whose ObjectType was classified (correctly) to
    /// its English canonical name "Charger". That literal comparison can
    /// never match across languages/synonyms, so these two features stayed
    /// at 0 even for a candidate that is a genuine, exact conceptual match -
    /// confirmed live: RankExplain showed ObjectMatch=+0.0/CategoryMatch=+0.0
    /// for a report matched against its own original text. Both features now
    /// ALSO check concept-level identity - the same mechanism
    /// IObjectTypeCompatibilityService already uses correctly for the
    /// "Same" judgment used elsewhere in ranking - as an addition to
    /// (not a replacement of) the original literal check, so the fix is
    /// generic: any two surface forms (any language, synonym, alias) that
    /// resolve to the same ontology concept now count as a match, without
    /// naming any specific object type or category anywhere in this class.
    /// </remarks>
    internal sealed class FeatureExtractor(
        IConceptRepository conceptRepository,
        IConceptResolver conceptResolver) : IFeatureExtractor
    {
        public async Task<RankingFeatures> ExtractAsync(
            FusedCandidate candidate, SearchableReport report, SemanticQuery query, CancellationToken cancellationToken = default)
        {
            var contributionsByStrategy = candidate.Contributions
                .GroupBy(c => c.StrategyName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Max(c => c.Score), StringComparer.Ordinal);

            double GetContribution(string strategyName) => contributionsByStrategy.GetValueOrDefault(strategyName, 0);
            double MatchedOrZero(string strategyName) => GetContribution(strategyName) > 0 ? 100.0 : 0.0;

            // ExactRetriever's raw score is the COUNT of query tokens found
            // verbatim in the candidate's text (see ExactRetriever.RetrieveAsync),
            // not a 0-or-1 flag - collapsing it through MatchedOrZero (as every
            // other free-text strategy here does) meant a query matching on a
            // single common word (e.g. just the color "black") scored an
            // identical, maximal 100 to a query whose every word matched,
            // regardless of the object itself being unrelated. Verified live
            // during PHASE-VALIDATION-04: an unrelated "black phone" report
            // scored ExactMatch=100 (and ~70% confidence) against a "black
            // hair dryer" query purely because "black" overlapped. Scaling by
            // the fraction of query tokens actually matched fixes this without
            // changing ExactRetriever/candidate generation at all.
            double ExactMatchFraction()
            {
                var matchedTokenCount = GetContribution("Exact");
                var totalQueryTokens = query.Tokens.Count;
                return totalQueryTokens > 0 ? Math.Clamp(matchedTokenCount / totalQueryTokens * 100.0, 0.0, 100.0) : 0.0;
            }

            var objectTypeEntity = query.Entities.FirstOrDefault(e => e.Type == EntityType.Object);
            var literalObjectTypeMatch = objectTypeEntity != null && MatchesField(report.ObjectType, objectTypeEntity.Value);
            var conceptObjectTypeMatch = await ObjectTypeMatchesResolvedConceptAsync(report, query, cancellationToken);

            // Real E2E finding (CreatorId/Ranking-Metadata validation): both
            // checks above depend on EntityRecognizer/IConceptResolver having
            // SOME ontology entry for the object type in question. A report
            // classified (by the AI provider) as ObjectType="Box" produced
            // ZERO recognized Object entities for a query whose own text
            // literally contained "box" multiple times - confirmed live via
            // RankExplain/diagnostic logging - because the seed ontology has
            // no "Box" concept at all, the same class of coverage gap already
            // found and partially addressed for RankingEngine's penalty path
            // (see ObjectTypeRelationship). Neither existing check can ever
            // fire for an object type the ontology simply doesn't know about,
            // no matter how literally it appears in the query. A last-resort,
            // ontology-independent fallback - do the candidate's ObjectType/
            // CategoryName words all appear as whole tokens somewhere in the
            // query's own token stream? - mirrors the same literal-text-
            // containment approach AliasMatch/MatchesAnyReportField already
            // use elsewhere in this class, just applied word-by-word (not a
            // raw substring check) so a short object type like "Car" can
            // never spuriously match inside an unrelated longer word like
            // "cardboard".
            var literalTokenObjectTypeMatch = WordsAppearAsTokens(report.ObjectType, query.Tokens);
            var objectTypeSimilarity = literalObjectTypeMatch || conceptObjectTypeMatch || literalTokenObjectTypeMatch
                ? 100.0
                : 0.0;

            var resolvedQueryConcept = query.ResolvedConceptId.HasValue
                ? await conceptRepository.GetByIdAsync(query.ResolvedConceptId.Value, cancellationToken)
                : null;
            var categoryMatchedOrZero = MatchedOrZero("Category");
            var categoryMatchesConcept = CategoryMatchesResolvedConcept(report, resolvedQueryConcept);
            var literalTokenCategoryMatch = WordsAppearAsTokens(report.CategoryName, query.Tokens);
            var categorySimilarity = categoryMatchedOrZero > 0 || categoryMatchesConcept || literalTokenCategoryMatch
                ? 100.0
                : 0.0;

            var aliasMatch = query.ExpandedTerms.Any(t => t.Reason == "Alias" && MatchesAnyReportField(report, t.Term))
                ? 100.0
                : 0.0;

            var features = new RankingFeatures(
                EmbeddingSimilarity: GetContribution("Vector"),
                MetadataEmbeddingSimilarity: GetContribution("MetadataVector"),
                Bm25Score: GetContribution("BM25"),
                KnowledgeGraphSimilarity: GetContribution("Graph"),
                ObjectTypeSimilarity: objectTypeSimilarity,
                CategorySimilarity: categorySimilarity,
                BrandSimilarity: MatchedOrZero("Brand"),
                ColorSimilarity: MatchedOrZero("Color"),
                MaterialSimilarity: MatchedOrZero("Material"),
                LocationSimilarity: MatchedOrZero("Location"),
                TimeProximity: GetContribution("Time") * 100.0, // TimeRetriever's raw score is already 0-1
                AliasMatch: aliasMatch,
                ExactMatch: ExactMatchFraction(),
                HistoricalSuccess: 0.0, // no real per-report match-outcome history exists yet - see RankingFeatures remarks
                Popularity: 0.0);

            return features;
        }

        // Resolves the CANDIDATE's stored ObjectType text to a concept (the
        // same alias/synonym/cross-language resolution QueryPipeline itself
        // used to resolve the query) and compares concept IDs - generic
        // concept-identity equality instead of literal text equality, so a
        // match is found regardless of which language or surface form
        // either side happens to be recorded in.
        private async Task<bool> ObjectTypeMatchesResolvedConceptAsync(
            SearchableReport report, SemanticQuery query, CancellationToken cancellationToken)
        {
            if (!query.ResolvedConceptId.HasValue || string.IsNullOrWhiteSpace(report.ObjectType))
            {
                return false;
            }

            var candidateConcept = await conceptResolver.ResolveAsync(report.ObjectType, query.LanguageCode, cancellationToken);
            return candidateConcept != null && candidateConcept.Id == query.ResolvedConceptId.Value;
        }

        // True when the candidate's business CategoryName is one of the
        // resolved query concept's OWN Categories - e.g. the query resolved
        // to "Charger" (Categories=["Electronics"]) and the candidate's
        // CategoryName is "Electronics". Generic: works for any concept/
        // category pairing the ontology defines, no category named here.
        private static bool CategoryMatchesResolvedConcept(SearchableReport report, Concept? resolvedQueryConcept) =>
            resolvedQueryConcept != null
            && !string.IsNullOrWhiteSpace(report.CategoryName)
            && resolvedQueryConcept.Categories.Any(category => string.Equals(category, report.CategoryName, StringComparison.OrdinalIgnoreCase));

        private static bool MatchesField(string? fieldValue, string term) =>
            !string.IsNullOrWhiteSpace(fieldValue)
            && string.Equals(fieldValue.Trim(), term.Trim(), StringComparison.OrdinalIgnoreCase);

        // Ontology-independent literal fallback (see ExtractAsync's remarks):
        // true only when EVERY word of fieldValue appears as its own WHOLE
        // token somewhere in queryTokens - never a raw substring check, so a
        // short field value ("Car") can't spuriously match inside an
        // unrelated longer token ("cardboard"). Order-independent (a
        // multi-word field value's words don't need to be adjacent or in the
        // same sequence in the query) since the goal is "did the user
        // mention this object", not phrase matching.
        private static bool WordsAppearAsTokens(string? fieldValue, IReadOnlyList<string> queryTokens)
        {
            if (string.IsNullOrWhiteSpace(fieldValue) || queryTokens.Count == 0)
            {
                return false;
            }

            var fieldWords = fieldValue.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return fieldWords.Length > 0 &&
                   fieldWords.All(word => queryTokens.Any(token => string.Equals(token, word, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool MatchesAnyReportField(SearchableReport report, string term)
        {
            var normalized = term.Trim();
            return MatchesField(report.ObjectType, normalized)
                || MatchesField(report.Color, normalized)
                || MatchesField(report.Brand, normalized)
                || report.Tags.Any(tag => MatchesField(tag, normalized));
        }
    }
}
