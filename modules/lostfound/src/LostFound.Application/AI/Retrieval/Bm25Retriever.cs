using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Standard Okapi BM25 (k1=1.5, b=0.75 - the commonly-used defaults) over
    // each candidate's searchable text (description, location details,
    // object type, color, brand, tags). Reuses Phase 2B Part 1's ITokenizer
    // rather than a third tokenization implementation.
    internal sealed class Bm25Retriever(ITokenizer tokenizer) : IBM25Retriever
    {
        private const double K1 = 1.5;
        private const double B = 0.75;

        public string StrategyName => "BM25";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var queryTerms = context.Query.Tokens.Distinct().ToList();
            if (queryTerms.Count == 0 || context.Candidates.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<StrategyCandidate>>(Array.Empty<StrategyCandidate>());
            }

            var documentTokens = context.Candidates.ToDictionary(r => r.ReportId, r => tokenizer.Tokenize(BuildSearchableText(r)));
            var totalDocs = context.Candidates.Count;
            var avgDocLength = documentTokens.Count == 0 ? 0 : documentTokens.Values.Average(t => t.Count);

            var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var tokens in documentTokens.Values)
            {
                foreach (var term in tokens.Distinct())
                {
                    documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
                }
            }

            var results = new List<StrategyCandidate>();

            foreach (var report in context.Candidates)
            {
                var tokens = documentTokens[report.ReportId];
                if (tokens.Count == 0)
                {
                    continue;
                }

                var termFrequency = tokens.GroupBy(t => t, StringComparer.Ordinal).ToDictionary(g => g.Key, g => (double)g.Count());
                double score = 0;

                foreach (var term in queryTerms)
                {
                    if (!termFrequency.TryGetValue(term, out var tf))
                    {
                        continue;
                    }

                    var df = documentFrequency.GetValueOrDefault(term, 0);
                    var idf = Math.Log(((totalDocs - df + 0.5) / (df + 0.5)) + 1);
                    var numerator = tf * (K1 + 1);
                    var denominator = tf + K1 * (1 - B + B * (tokens.Count / avgDocLength));
                    score += idf * (numerator / denominator);
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
                new[] { report.Description, report.LocationDetails, report.ObjectType, report.Color, report.Brand }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Concat(report.Tags));
    }
}
