using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Levenshtein-distance fuzzy matching between query tokens and each
    // candidate's searchable text tokens - catches typos/spelling variants
    // the exact/BM25 retrievers would miss entirely. Shares
    // LostFound.AI.TextSimilarity.LevenshteinDistance with Phase 2B Part 1's
    // DictionarySpellCorrectionService rather than a second implementation.
    internal sealed class FuzzyRetriever(ITokenizer tokenizer) : IFuzzyRetriever
    {
        private const int MaxEditDistance = 2;

        public string StrategyName => "Fuzzy";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var queryTerms = context.Query.Tokens.Where(t => t.Length >= 3).Distinct().ToList();
            if (queryTerms.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<StrategyCandidate>>(Array.Empty<StrategyCandidate>());
            }

            var results = new List<StrategyCandidate>();

            foreach (var report in context.Candidates)
            {
                var reportTokens = tokenizer.Tokenize(BuildSearchableText(report));
                if (reportTokens.Count == 0)
                {
                    continue;
                }

                double score = 0;

                foreach (var queryTerm in queryTerms)
                {
                    var bestDistance = reportTokens
                        .Where(t => Math.Abs(t.Length - queryTerm.Length) <= MaxEditDistance)
                        .Select(t => TextSimilarity.LevenshteinDistance(queryTerm, t))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min();

                    if (bestDistance <= MaxEditDistance)
                    {
                        score += 1.0 - (double)bestDistance / Math.Max(queryTerm.Length, 1);
                    }
                }

                if (score > 0)
                {
                    results.Add(new StrategyCandidate(report.ReportId, score));
                }
            }

            return Task.FromResult<IReadOnlyList<StrategyCandidate>>(
                results.OrderByDescending(c => c.Score).Take(context.Limit).ToList());
        }

        private static string BuildSearchableText(SearchableReport report) =>
            string.Join(
                " ",
                new[] { report.Description, report.ObjectType, report.Color, report.Brand }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Concat(report.Tags));
    }
}
