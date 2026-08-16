using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Four small, structurally identical attribute-similarity strategies -
    // each matches Phase 2B Part 1's recognized entities of one EntityType
    // against the corresponding SearchableReport field. Not given their own
    // dedicated interfaces (unlike the five core retrievers) since the spec
    // only names Vector/BM25/Graph/Fuzzy/Exact as needing one - these
    // implement IRetrievalStrategy directly.
    //
    // PHASE-VALIDATION-08: Category/Brand/Color now match by CONCEPT
    // IDENTITY (via ConceptTextMatcher/IConceptResolver), not just literal
    // text - see ConceptTextMatcher's remarks. Real, live symptom this
    // fixes: a Gemini-classified report stored Brand="Canon" never matched
    // a query recognizing "كانون" even though both denote the exact same
    // ontology concept, because the two strings are literally different.

    internal sealed class CategoryRetriever(IConceptResolver conceptResolver) : IRetrievalStrategy
    {
        public string StrategyName => "Category";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default) =>
            AttributeMatchHelper.MatchEntityAgainstFieldAsync(context, EntityType.Category, report => report.CategoryName, conceptResolver, cancellationToken);
    }

    internal sealed class BrandRetriever(IConceptResolver conceptResolver) : IRetrievalStrategy
    {
        public string StrategyName => "Brand";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default) =>
            AttributeMatchHelper.MatchEntityAgainstFieldAsync(context, EntityType.Brand, report => report.Brand, conceptResolver, cancellationToken);
    }

    internal sealed class ColorRetriever(IConceptResolver conceptResolver) : IRetrievalStrategy
    {
        public string StrategyName => "Color";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default) =>
            AttributeMatchHelper.MatchEntityAgainstFieldAsync(context, EntityType.Color, report => report.Color, conceptResolver, cancellationToken);
    }

    // Report has no dedicated Material field (unlike Color/Brand) - matches
    // against Tags instead, a real but weaker signal since materials
    // ("leather", "stainless steel") only appear there when a tag happens
    // to name one, not as structured data. Honestly approximate, not faked.
    // Left on literal tag matching (not concept-identity) - Tags is a free
    // list, not a single structured field, and resolving every tag on every
    // candidate would be a materially bigger cost for a signal already
    // documented as a weaker approximation.
    internal sealed class MaterialRetriever : IRetrievalStrategy
    {
        public string StrategyName => "Material";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var materialTerms = AttributeMatchHelper.ExtractEntityValues(context.Query, EntityType.Material);
            if (materialTerms.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<StrategyCandidate>>(Array.Empty<StrategyCandidate>());
            }

            var results = context.Candidates
                .Where(r => r.Tags.Any(tag => materialTerms.Contains(tag.Trim().ToLowerInvariant())))
                .Select(r => new StrategyCandidate(r.ReportId, 1.0))
                .Take(context.Limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<StrategyCandidate>>(results);
        }
    }

    internal static class AttributeMatchHelper
    {
        public static HashSet<string> ExtractEntityValues(SemanticQuery query, EntityType type) =>
            query.Entities
                .Where(e => e.Type == type)
                .Select(e => e.Value.Trim().ToLowerInvariant())
                .Where(v => v.Length > 0)
                .ToHashSet(StringComparer.Ordinal);

        public static async Task<IReadOnlyList<StrategyCandidate>> MatchEntityAgainstFieldAsync(
            RetrievalContext context,
            EntityType type,
            Func<SearchableReport, string?> fieldSelector,
            IConceptResolver conceptResolver,
            CancellationToken cancellationToken)
        {
            var entityValues = ExtractEntityValues(context.Query, type);
            if (entityValues.Count == 0)
            {
                return Array.Empty<StrategyCandidate>();
            }

            var results = new List<StrategyCandidate>();

            foreach (var report in context.Candidates)
            {
                if (results.Count >= context.Limit)
                {
                    break;
                }

                var fieldValue = fieldSelector(report);
                if (string.IsNullOrWhiteSpace(fieldValue))
                {
                    continue;
                }

                var matched = false;
                foreach (var entityValue in entityValues)
                {
                    if (await ConceptTextMatcher.AreSameAsync(entityValue, fieldValue, context.Query.LanguageCode, conceptResolver, cancellationToken))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    results.Add(new StrategyCandidate(report.ReportId, 1.0));
                }
            }

            return results;
        }
    }
}
