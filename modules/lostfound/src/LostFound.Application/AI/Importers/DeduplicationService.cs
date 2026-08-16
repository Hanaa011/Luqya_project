using System;
using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Importers
{
    /// <summary>
    /// Three-tier duplicate detection, matching the spec's "Deduplication
    /// Strategy" list:
    /// <list type="number">
    /// <item>Exact - records sharing a (SourceDataset, SourceId) pair, e.g.
    /// the same Wikidata QID's English and Arabic labels arriving as two
    /// records.</item>
    /// <item>Alias - identical canonical name (case-insensitive) within the
    /// same language, across different source identifiers (language-aware
    /// duplicate detection, per the spec).</item>
    /// <item>Semantic - a token-overlap (Jaccard) similarity heuristic
    /// within the same language. This is a deliberate, documented
    /// placeholder: real semantic duplicate detection belongs on top of
    /// Phase 2A Part 2's embedding engine, which currently has no local
    /// model installed (falls back to the external provider). Swapping
    /// this tier for real embedding-similarity comparison once a model is
    /// installed requires no change to ICanonicalizer or IImportCoordinator -
    /// only this class.</item>
    /// </list>
    /// </summary>
    internal sealed class DeduplicationService : IDeduplicationService
    {
        private const double SemanticSimilarityThreshold = 0.8;

        public IReadOnlyList<DuplicateGroup> GroupDuplicates(IReadOnlyList<RawConceptRecord> records)
        {
            var groups = new List<DuplicateGroup>();
            var claimed = new HashSet<RawConceptRecord>();

            AddGroups(
                groups, claimed,
                records.Where(r => !claimed.Contains(r)).GroupBy(r => (r.SourceDataset, r.SourceId)).Where(g => g.Count() > 1),
                DuplicateMatchKind.Exact,
                key => $"Shared source identifier '{key.SourceId}' in dataset '{key.SourceDataset}'.");

            AddGroups(
                groups, claimed,
                records.Where(r => !claimed.Contains(r))
                    .GroupBy(r => (Name: r.CanonicalName.Trim().ToLowerInvariant(), r.LanguageCode))
                    .Where(g => g.Count() > 1),
                DuplicateMatchKind.Alias,
                key => $"Identical canonical name '{key.Name}' ({key.LanguageCode}).");

            var remaining = records.Where(r => !claimed.Contains(r)).ToList();
            for (var i = 0; i < remaining.Count; i++)
            {
                if (claimed.Contains(remaining[i]))
                {
                    continue;
                }

                var cluster = new List<RawConceptRecord> { remaining[i] };

                for (var j = i + 1; j < remaining.Count; j++)
                {
                    if (claimed.Contains(remaining[j]) || remaining[i].LanguageCode != remaining[j].LanguageCode)
                    {
                        continue;
                    }

                    if (JaccardSimilarity(remaining[i].CanonicalName, remaining[j].CanonicalName) >= SemanticSimilarityThreshold)
                    {
                        cluster.Add(remaining[j]);
                        claimed.Add(remaining[j]);
                    }
                }

                claimed.Add(remaining[i]);

                groups.Add(cluster.Count > 1
                    ? new DuplicateGroup(cluster, DuplicateMatchKind.Semantic,
                        $"Token-overlap similarity >= {SemanticSimilarityThreshold:P0} (heuristic fallback - no local embedding model installed).")
                    : new DuplicateGroup(cluster, DuplicateMatchKind.Exact, "No duplicate found."));
            }

            return groups;
        }

        private static void AddGroups<TKey>(
            List<DuplicateGroup> groups,
            HashSet<RawConceptRecord> claimed,
            IEnumerable<IGrouping<TKey, RawConceptRecord>> buckets,
            DuplicateMatchKind kind,
            Func<TKey, string> describeReason)
            where TKey : notnull
        {
            foreach (var bucket in buckets)
            {
                var bucketRecords = bucket.ToList();
                groups.Add(new DuplicateGroup(bucketRecords, kind, describeReason(bucket.Key)));

                foreach (var record in bucketRecords)
                {
                    claimed.Add(record);
                }
            }
        }

        private static double JaccardSimilarity(string a, string b)
        {
            var tokensA = Tokenize(a);
            var tokensB = Tokenize(b);

            if (tokensA.Count == 0 || tokensB.Count == 0)
            {
                return 0;
            }

            return (double)tokensA.Intersect(tokensB).Count() / tokensA.Union(tokensB).Count();
        }

        private static HashSet<string> Tokenize(string text) =>
            text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }
}
