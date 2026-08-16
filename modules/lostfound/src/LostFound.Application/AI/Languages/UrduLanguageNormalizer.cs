using System.Text;

namespace LostFound.AI.Languages
{
    // Urdu rules per the Part 3 spec: Unicode normalization + character
    // normalization. Urdu shares the Arabic script but has its own
    // presentation-form variants (e.g. Urdu Yeh U+06CC vs Arabic Yeh
    // U+064A, Urdu Kaf U+06A9 vs Arabic Kaf U+0643) that need normalizing to
    // a single canonical form independent of Arabic's rules - kept as its
    // own class (not reusing ArabicLanguageNormalizer) since conflating the
    // two scripts' normalization tables is exactly the kind of
    // language-specific detail Principle 5 ("adding a language must never
    // require architectural redesign") means to keep isolated.
    internal sealed class UrduLanguageNormalizer : ILanguageNormalizer
    {
        public string LanguageCode => "ur";

        public string Normalize(string text)
        {
            var formC = text.Trim().Normalize(System.Text.NormalizationForm.FormC);
            var builder = new StringBuilder(formC.Length);

            foreach (var ch in formC)
            {
                builder.Append(NormalizeChar(ch));
            }

            return builder.ToString();
        }

        private static char NormalizeChar(char ch) => ch switch
        {
            'ي' => 'ی', // Arabic Yeh -> Urdu Yeh
            'ك' => 'ک', // Arabic Kaf -> Urdu Kaf (Keheh)
            'ہ' => 'ہ', // Urdu Heh Goal (kept as-is, listed for clarity)
            _ => ch
        };
    }
}
