using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.Matching;

namespace LostFound.AI.Retrieval
{
    // Collapses candidates whose stored embeddings are near-identical
    // (>= SemanticDuplicateThreshold cosine-similarity percentage) into one,
    // combining both candidates' provenance ("Preserve provenance from all
    // retrieval sources" - spec) rather than discarding either. Report-ID
    // duplication is already impossible after ICandidateMerger (that IS the
    // merge key); Concept-ID dedup is not implemented - Report has no
    // Concept linkage yet (Phase 2B Part 4 territory).
    internal sealed class DuplicateResolver(IOptions<RetrievalOptions> options) : IDuplicateResolver
    {
        public IReadOnlyList<RetrievedCandidate> Resolve(
            IReadOnlyList<RetrievedCandidate> candidates, IReadOnlyDictionary<Guid, SearchableReport> reportsById)
        {
            var threshold = options.Value.SemanticDuplicateThreshold;
            var claimed = new HashSet<Guid>();
            var merged = new List<RetrievedCandidate>();

            foreach (var candidate in candidates)
            {
                if (!claimed.Add(candidate.ReportId))
                {
                    continue;
                }

                var group = new List<RetrievedCandidate> { candidate };

                if (reportsById.TryGetValue(candidate.ReportId, out var report) && report.EmbeddingVector is { Length: > 0 })
                {
                    foreach (var other in candidates)
                    {
                        if (claimed.Contains(other.ReportId))
                        {
                            continue;
                        }

                        if (!reportsById.TryGetValue(other.ReportId, out var otherReport) || otherReport.EmbeddingVector is not { Length: > 0 })
                        {
                            continue;
                        }

                        var similarity = CosineSimilarityCalculator.CalculatePercentage(report.EmbeddingVector, otherReport.EmbeddingVector);
                        if (similarity >= threshold)
                        {
                            group.Add(other);
                            claimed.Add(other.ReportId);
                        }
                    }
                }

                merged.Add(group.Count == 1 ? group[0] : MergeGroup(group));
            }

            return merged;
        }

        private static RetrievedCandidate MergeGroup(List<RetrievedCandidate> group)
        {
            // Keep the identity of whichever group member has the strongest
            // combined signal, but carry every member's provenance forward.
            var primary = group.OrderByDescending(c => c.Contributions.Sum(x => x.Score)).First();
            var allContributions = group.SelectMany(c => c.Contributions).ToList();

            return primary with { Contributions = allContributions };
        }
    }
}
