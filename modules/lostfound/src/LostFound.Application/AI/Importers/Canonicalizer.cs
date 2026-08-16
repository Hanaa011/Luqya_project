using System;
using System.Collections.Generic;
using System.Linq;
using LostFound.AI.Concepts;

namespace LostFound.AI.Importers
{
    internal sealed class Canonicalizer : ICanonicalizer
    {
        public Concept Canonicalize(DuplicateGroup group)
        {
            var records = group.Records;

            // Order-independent so the same source data always produces the
            // same ConceptId across re-imports, regardless of which record
            // in the group happens to be picked as "primary" below - see
            // DeterministicGuid's remarks on why this matters for resumable
            // imports.
            var idSeed = string.Join(
                '|', records.Select(r => $"{r.SourceDataset}:{r.SourceId}").OrderBy(s => s, StringComparer.Ordinal));
            var id = DeterministicGuid.From(idSeed);

            var primary = records
                .OrderByDescending(r => r.Confidence)
                .ThenBy(r => r.CanonicalName, StringComparer.Ordinal)
                .First();

            var localizedNames = records
                .GroupBy(r => r.LanguageCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().CanonicalName, StringComparer.OrdinalIgnoreCase);

            return new Concept
            {
                Id = id,
                CanonicalName = primary.CanonicalName,
                LocalizedNames = localizedNames,
                Synonyms = MergeByLanguage(records, r => r.Synonyms),
                Aliases = MergeByLanguage(records, r => r.Aliases),
                DialectWords = MergeByLanguage(records, r => r.DialectWords),
                CommonMisspellings = MergeByLanguage(records, r => r.CommonMisspellings),
                Categories = records.SelectMany(r => r.Categories).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                LanguageAvailability = records.Select(r => r.LanguageCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                SourceDataset = string.Join(",", records.Select(r => r.SourceDataset).Distinct(StringComparer.OrdinalIgnoreCase)),
                ConfidenceScore = records.Max(r => r.Confidence),
                ImportedAtUtc = DateTime.UtcNow
            };
        }

        private static Dictionary<string, IReadOnlyList<string>> MergeByLanguage(
            IReadOnlyList<RawConceptRecord> records, Func<RawConceptRecord, IReadOnlyList<string>> selector) =>
            records
                .GroupBy(r => r.LanguageCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<string>)g.SelectMany(selector).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    StringComparer.OrdinalIgnoreCase);
    }
}
