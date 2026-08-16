using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Matches recognized Location entities (Phase 2B Part 1) against
    // Report.LocationDetails free text - the only location data available
    // on SearchableReport (Report has a required LocationId FK to a
    // Location aggregate with just a PlaceName, and no geo/lat-long field
    // anywhere in the domain - real proximity-based location retrieval
    // isn't possible without that data, so this is text-containment only).
    internal sealed class LocationRetriever : IRetrievalStrategy
    {
        public string StrategyName => "Location";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var locationTerms = AttributeMatchHelper.ExtractEntityValues(context.Query, EntityType.Location);
            if (locationTerms.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<StrategyCandidate>>(Array.Empty<StrategyCandidate>());
            }

            var results = context.Candidates
                .Where(r => !string.IsNullOrWhiteSpace(r.LocationDetails)
                            && locationTerms.Any(term => r.LocationDetails!.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(r => new StrategyCandidate(r.ReportId, 1.0))
                .Take(context.Limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<StrategyCandidate>>(results);
        }
    }

    // Time proximity: scores candidates higher the closer their
    // LostFoundDate is to a DateTime entity recognized in the query (or, if
    // none was recognized, does nothing rather than guessing "recent" -
    // there is no real signal to rank by without an explicit date).
    // Recognized dates are numeric patterns only (Phase 2B Part 1's
    // EntityRecognizer) - genuine natural-language date parsing ("last
    // Tuesday") is not implemented.
    internal sealed class TimeRetriever : IRetrievalStrategy
    {
        private static readonly TimeSpan ProximityWindow = TimeSpan.FromDays(30);

        public string StrategyName => "Time";

        public Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var dateEntity = context.Query.Entities.FirstOrDefault(e => e.Type == EntityType.DateTime);
            if (dateEntity == null || !DateTime.TryParse(dateEntity.Value, out var targetDate))
            {
                return Task.FromResult<IReadOnlyList<StrategyCandidate>>(Array.Empty<StrategyCandidate>());
            }

            var results = new List<StrategyCandidate>();

            foreach (var report in context.Candidates)
            {
                if (report.LostFoundDate is not { } reportDate)
                {
                    continue;
                }

                var distance = (reportDate - targetDate).Duration();
                if (distance > ProximityWindow)
                {
                    continue;
                }

                var score = 1.0 - distance.TotalDays / ProximityWindow.TotalDays;
                results.Add(new StrategyCandidate(report.ReportId, score));
            }

            return Task.FromResult<IReadOnlyList<StrategyCandidate>>(
                results.OrderByDescending(c => c.Score).Take(context.Limit).ToList());
        }
    }
}
