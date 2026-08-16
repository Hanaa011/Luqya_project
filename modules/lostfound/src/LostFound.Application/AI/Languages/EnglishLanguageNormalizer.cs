using System;
using System.Globalization;

namespace LostFound.AI.Languages
{
    // English rules per the Part 3 spec: lowercase, plural normalization.
    // Full lemmatization (spec also lists this) needs a real
    // morphological-analysis dictionary/model to do correctly for irregular
    // forms (e.g. "mice" -> "mouse") - not something to fake with a regex
    // and claim correctness. This implements the mechanical, verifiably
    // correct subset (lowercasing + regular -s/-es plural stripping) and
    // documents full lemmatization as a follow-up once a real dataset
    // (Phase 2A Part 4) is available to drive it, rather than shipping
    // silently-wrong heuristics.
    internal sealed class EnglishLanguageNormalizer : ILanguageNormalizer
    {
        public string LanguageCode => "en";

        public string Normalize(string text)
        {
            var lowered = text.Trim().ToLower(CultureInfo.InvariantCulture);
            return StripRegularPlural(lowered);
        }

        private static string StripRegularPlural(string word)
        {
            if (word.Length > 4 && word.EndsWith("ies"))
            {
                return string.Concat(word.AsSpan(0, word.Length - 3), "y");
            }

            if (word.Length > 4 && (word.EndsWith("xes") || word.EndsWith("ses") || word.EndsWith("shes") || word.EndsWith("ches")))
            {
                return word[..^2];
            }

            if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss"))
            {
                return word[..^1];
            }

            return word;
        }
    }
}
