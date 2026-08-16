using System;
using System.Linq;
using System.Text;

namespace LostFound.AI.Importers
{
    // Light, display-preserving cleanup (Unicode NFC normalization +
    // whitespace collapsing) - deliberately NOT the same as
    // LostFound.AI.Concepts.IConceptNormalizer's language-specific MATCHING
    // rules (diacritic stripping, lowercasing, etc.), which would destroy
    // meaningful display text. Matching-normalization is applied later, at
    // alias-index build time (see InMemoryAliasResolver), not here - this
    // stage only needs to make raw source data consistent enough to compare
    // reliably in the pipeline stages that follow (dedup, canonicalization).
    internal sealed class DataNormalizer : IDataNormalizer
    {
        public RawConceptRecord Normalize(RawConceptRecord record) => record with
        {
            CanonicalName = Clean(record.CanonicalName),
            Synonyms = record.Synonyms.Select(Clean).Where(s => s.Length > 0).ToList(),
            Aliases = record.Aliases.Select(Clean).Where(s => s.Length > 0).ToList(),
            DialectWords = record.DialectWords.Select(Clean).Where(s => s.Length > 0).ToList(),
            CommonMisspellings = record.CommonMisspellings.Select(Clean).Where(s => s.Length > 0).ToList(),
            Categories = record.Categories.Select(Clean).Where(s => s.Length > 0).ToList(),
            ParentNames = record.ParentNames.Select(Clean).Where(s => s.Length > 0).ToList()
        };

        private static string Clean(string text) =>
            string.Join(
                ' ',
                text.Normalize(NormalizationForm.FormC)
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
