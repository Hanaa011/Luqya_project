using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Retrieval
{
    // Matches candidates against Phase 2B Part 1's knowledge-graph
    // expansion terms (Concept synonyms/aliases/dialect words/misspellings
    // plus IsA/SimilarTo/RelatedTo neighbors - see ISemanticExpander).
    // Score is the sum of each matched term's own expansion Weight, so a
    // direct Synonym/Alias match (weight ~0.85-0.9) counts for more than a
    // loosely-related RelatedConcept match (weight 0.5).
    internal sealed class GraphRetriever : IGraphRetriever
    {
        public string StrategyName => "Graph";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var expandedTermsByText = context.Query.ExpandedTerms
                .Where(t => !string.IsNullOrWhiteSpace(t.Term))
                .GroupBy(t => t.Term.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Max(t => t.Weight), StringComparer.Ordinal);

            if (expandedTermsByText.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<StrategyCandidate>>(Array.Empty<StrategyCandidate>());
            }

            var results = new List<StrategyCandidate>();

            foreach (var report in context.Candidates)
            {
                var reportTerms = BuildReportTerms(report);
                var score = reportTerms.Sum(term => expandedTermsByText.GetValueOrDefault(term, 0));

                if (score > 0)
                {
                    results.Add(new StrategyCandidate(report.ReportId, score));
                }
            }

            return Task.FromResult<IReadOnlyList<StrategyCandidate>>(
                results.OrderByDescending(c => c.Score).Take(context.Limit).ToList());
        }

        private static HashSet<string> BuildReportTerms(SearchableReport report)
        {
            var terms = new HashSet<string>(StringComparer.Ordinal);

            void Add(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    terms.Add(value.Trim().ToLowerInvariant());
                }
            }

            Add(report.ObjectType);
            Add(report.Color);
            Add(report.Brand);

            foreach (var tag in report.Tags)
            {
                Add(tag);
            }

            return terms;
        }
    }
}
