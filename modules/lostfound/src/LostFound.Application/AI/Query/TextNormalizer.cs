using System.Text;

namespace LostFound.AI.Query
{
    // Language-agnostic pass: Unicode NFC form, Arabic letter-form
    // collapsing (hamza variants, alef maksura, taa marbuta - the same
    // table the pre-Phase-2B SearchTextProcessor had inline), diacritic/
    // tatweel stripping, and punctuation-to-space. Runs on every query
    // regardless of detected language, since mixed-script queries are real
    // ("gold ايفون").
    internal sealed class TextNormalizer : ITextNormalizer
    {
        public string Normalize(string text)
        {
            var formC = text.Normalize(System.Text.NormalizationForm.FormC);
            var builder = new StringBuilder(formC.Length);

            foreach (var ch in formC)
            {
                switch (ch)
                {
                    // Diacritics / tatweel - drop entirely.
                    case 'ً': case 'ٌ': case 'ٍ': case 'َ':
                    case 'ُ': case 'ِ': case 'ّ': case 'ْ':
                    case 'ـ':
                        continue;

                    // Hamza forms -> bare alef.
                    case 'أ': case 'إ': case 'آ': case 'ٱ':
                        builder.Append('ا');
                        continue;

                    case 'ؤ':
                        builder.Append('و');
                        continue;
                    case 'ئ':
                        builder.Append('ي');
                        continue;

                    // Alef maksura -> yaa.
                    case 'ى':
                        builder.Append('ي');
                        continue;

                    // Taa marbuta -> haa.
                    case 'ة':
                        builder.Append('ه');
                        continue;

                    default:
                        builder.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
                        continue;
                }
            }

            return builder.ToString();
        }
    }
}
