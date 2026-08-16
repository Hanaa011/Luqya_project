using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LostFound.AI.Query
{
    // Fixed-table Arabic -> Latin phonetic approximation, covering the
    // standard Arabic consonant/vowel-letter set. NOT a real transliteration
    // standard (not full Buckwalter/ALA-LC) and not bidirectional (no
    // Latin -> Arabic table - going the other way needs disambiguation this
    // simple table can't do, e.g. Latin "s" could be س or ص). Good enough to
    // catch an Arabic-script brand name matching its common Latin spelling
    // (e.g. "ايفون" -> approximately "ayfwn", closer to "iphone" than the
    // untransliterated Arabic is to any Latin string) - not linguistically
    // precise.
    internal sealed class TransliterationService : ITransliterationService
    {
        private static readonly IReadOnlyDictionary<char, string> ArabicToLatin = new Dictionary<char, string>
        {
            ['ا'] = "a", ['ب'] = "b", ['ت'] = "t", ['ث'] = "th", ['ج'] = "j",
            ['ح'] = "h", ['خ'] = "kh", ['د'] = "d", ['ذ'] = "dh", ['ر'] = "r",
            ['ز'] = "z", ['س'] = "s", ['ش'] = "sh", ['ص'] = "s", ['ض'] = "d",
            ['ط'] = "t", ['ظ'] = "z", ['ع'] = "a", ['غ'] = "gh", ['ف'] = "f",
            ['ق'] = "q", ['ك'] = "k", ['ل'] = "l", ['م'] = "m", ['ن'] = "n",
            ['ه'] = "h", ['و'] = "w", ['ي'] = "y", ['ء'] = "'"
        };

        public string Transliterate(string text, string sourceLanguageCode)
        {
            if (sourceLanguageCode is not ("ar" or "ur"))
            {
                return text; // no Latin -> Arabic direction - see class remarks
            }

            var builder = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                builder.Append(ArabicToLatin.TryGetValue(ch, out var latin) ? latin : ch.ToString());
            }

            return builder.ToString();
        }
    }
}
