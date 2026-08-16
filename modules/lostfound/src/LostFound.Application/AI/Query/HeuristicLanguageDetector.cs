using System.Linq;

namespace LostFound.AI.Query
{
    // Character-range heuristic: Latin script -> "en"; Arabic script ->
    // "ar" or "ur" depending on whether Urdu-specific letters are present.
    // Reliable for Latin-vs-Arabic-script (unambiguous Unicode ranges);
    // HONESTLY APPROXIMATE for Arabic-vs-Urdu, since both share the Arabic
    // script and a short query may contain no Urdu-exclusive character at
    // all (e.g. "موبائل" uses only letters Arabic also has) - defaults to
    // "ar" in that case. True Arabic/Urdu disambiguation needs a real
    // language-ID model, not a character-range check.
    internal sealed class HeuristicLanguageDetector : ILanguageDetector
    {
        // Letters that exist in Urdu's extended Arabic-script alphabet but
        // not in standard Arabic: ٹ (tteh), ڈ (ddal), ڑ (rreh), ں (noon
        // ghunna), ے (barree yeh), پ (peh), چ (tcheh), گ (gaf).
        private static readonly char[] UrduExclusiveLetters = { 'ٹ', 'ڈ', 'ڑ', 'ں', 'ے', 'پ', 'چ', 'گ' };

        public LanguageDetectionResult Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new LanguageDetectionResult("en", 0.0);
            }

            var arabicScriptCount = 0;
            var latinScriptCount = 0;
            var urduExclusiveCount = 0;

            foreach (var ch in text)
            {
                if (UrduExclusiveLetters.Contains(ch))
                {
                    urduExclusiveCount++;
                    arabicScriptCount++;
                }
                else if (ch is >= '؀' and <= 'ۿ')
                {
                    arabicScriptCount++;
                }
                else if (char.IsLetter(ch) && ch < 128)
                {
                    latinScriptCount++;
                }
            }

            if (arabicScriptCount == 0 && latinScriptCount == 0)
            {
                return new LanguageDetectionResult("en", 0.3);
            }

            if (arabicScriptCount > latinScriptCount)
            {
                return urduExclusiveCount > 0
                    ? new LanguageDetectionResult("ur", 0.7)
                    : new LanguageDetectionResult("ar", 0.6); // could genuinely be Urdu - see class remarks
            }

            return new LanguageDetectionResult("en", 0.8);
        }
    }
}
