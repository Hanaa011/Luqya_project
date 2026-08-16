using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Exact token-overlap matching (identifier/alias-style matching per the
    // spec's "Exact identifier matching, Alias matching" strategies) -
    // deliberately the highest-confidence, lowest-recall signal: a report
    // that literally contains a query word is very likely relevant, with no
    // approximation involved.
    internal sealed class ExactRetriever(ITokenizer tokenizer) : IExactRetriever
    {
        public string StrategyName => "Exact";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var queryTerms = new HashSet<string>(context.Query.Tokens, StringComparer.Ordinal);
            if (queryTerms.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<StrategyCandidate>>(Array.Empty<StrategyCandidate>());
            }

            var results = new List<StrategyCandidate>();

            foreach (var report in context.Candidates)
            {
                var reportTokens = tokenizer.Tokenize(BuildSearchableText(report));
                var matches = reportTokens.Count(queryTerms.Contains);

                if (matches > 0)
                {
                    results.Add(new StrategyCandidate(report.ReportId, matches));
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
