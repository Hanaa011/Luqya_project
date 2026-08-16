using System.Text;

namespace LostFound.AI.Languages
{
    // Arabic rules per the Part 3 spec: remove diacritics (tashkeel), remove
    // tatweel (kashida), normalize Alef variants to bare Alef, normalize Ya
    // variants (including Alef Maksura) to Ya, normalize punctuation. These
    // are the standard, well-established Arabic search-normalization rules
    // (the same family implemented by e.g. Lucene's ArabicNormalizer) -
    // deterministic Unicode-range operations, not a heuristic guess.
    internal sealed class ArabicLanguageNormalizer : ILanguageNormalizer
    {
        public string LanguageCode => "ar";

        private const char Tatweel = 'ـ';

        public string Normalize(string text)
        {
            var builder = new StringBuilder(text.Length);

            foreach (var ch in text.Trim())
            {
                if (IsDiacritic(ch) || ch == Tatweel)
                {
                    continue;
                }

                builder.Append(NormalizeChar(ch));
            }

            return builder.ToString();
        }

        // Arabic diacritics (tashkeel/harakat) occupy U+064B-U+065F and
        // U+0670 (superscript Alef) - stripping them lets differently-
        // vocalized spellings of the same word match.
        private static bool IsDiacritic(char ch) => ch is >= 'ً' and <= 'ٟ' or 'ٰ';

        private static char NormalizeChar(char ch) => ch switch
        {
            'آ' or 'أ' or 'إ' or 'ٱ' => 'ا', // Alef with Madda/Hamza/Wasla -> bare Alef
            'ى' => 'ي',                                     // Alef Maksura -> Ya
            'ة' => 'ه',                                     // Teh Marbuta -> Heh (common search-normalization convention)
            _ => ch
        };
    }
}
