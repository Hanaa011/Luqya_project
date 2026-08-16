using System.Collections.Generic;
using System.Text;

namespace LostFound.AI
{
    /// <summary>
    /// Builds the natural-language, multilingual <c>MatchExplanation</c>
    /// paragraph for a single search result entirely from facts the
    /// application already has (<see cref="ReasonSummary"/>) - the score,
    /// which attributes matched, and the common tags. This is the single
    /// replacement for what used to be a second LLM round trip
    /// (<c>IMatchExplanationProvider</c>, one Gemini/OpenAI/Ollama/DeepSeek/
    /// HuggingFace call per result, ~15-20s total) that existed purely to
    /// phrase a sentence out of facts the caller already knew.
    ///
    /// Contract:
    /// <list type="bullet">
    /// <item>Provider-independent: no HTTP client, no API key, no model name.</item>
    /// <item>No HTTP requests, no calls to any AI model.</item>
    /// <item>Deterministic: the same <see cref="ReasonSummary"/> always
    /// produces the same explanation string.</item>
    /// <item>Never mentions a fact that is missing/empty - Brand, Color,
    /// ObjectType, and Tags are only mentioned when actually present.</item>
    /// <item>Mirrors the search language: replies in Arabic when the search
    /// text is Arabic, otherwise in English (see <see cref="LooksArabic"/>).
    /// This mirrors the language rule the old LLM prompt used to enforce,
    /// without needing a model call to do it.</item>
    /// </list>
    /// </summary>
    internal static class MatchExplanationGenerator
    {
        /// <summary>
        /// Thresholds used purely to phrase the explanation (e.g. "highly
        /// similar" vs "moderately similar"). Deliberately independent of
        /// <c>AiMatchingService.ReasonThresholds</c> - this class must stay
        /// fully self-contained so it can be reused/tested without pulling
        /// in the scoring service - but the values are kept in lockstep with
        /// it (also 70 / 40) since they describe the same score.
        /// </summary>
        private static class Thresholds
        {
            public const double High = 70;
            public const double Moderate = 40;
        }

        public static string Build(ReasonSummary summary)
        {
            var isArabic = LooksArabic(summary.Search.SearchText) || LooksArabic(summary.Candidate.Description);

            return isArabic ? BuildArabic(summary) : BuildEnglish(summary);
        }

        /// <summary>
        /// True if the text contains at least one character in the Arabic
        /// Unicode block. Good enough to route "reply in the user's
        /// language" without needing a full language-detection library -
        /// the old LLM prompt made the same binary Arabic/English choice.
        /// </summary>
        private static bool LooksArabic(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var ch in text)
            {
                if (ch >= '\u0600' && ch <= '\u06FF')
                {
                    return true;
                }
            }

            return false;
        }

        // =====================================================================
        // ---- English --------------------------------------------------------
        // =====================================================================

        private static string BuildEnglish(ReasonSummary summary)
        {
            var scoring = summary.Scoring;
            var candidate = summary.Candidate;

            var sb = new StringBuilder(BuildEnglishOpening(scoring));

            var attributeClauses = new List<string>(4);

            if (scoring.ObjectTypeMatched)
            {
                attributeClauses.Add(!string.IsNullOrWhiteSpace(candidate.ObjectType)
                    ? $"the object type ({candidate.ObjectType})"
                    : "the object type");
            }

            if (scoring.ColorMatched)
            {
                attributeClauses.Add(!string.IsNullOrWhiteSpace(candidate.Color)
                    ? $"the color ({candidate.Color})"
                    : "the color");
            }

            if (scoring.BrandMatched)
            {
                attributeClauses.Add(!string.IsNullOrWhiteSpace(candidate.Brand)
                    ? $"the brand ({candidate.Brand})"
                    : "the brand");
            }

            if (attributeClauses.Count > 0)
            {
                sb.Append(" Also, ");
                sb.Append(JoinNaturally(attributeClauses));
                sb.Append(attributeClauses.Count == 1 ? " also matches" : " also match");

                if (scoring.CommonTags.Count > 0)
                {
                    sb.Append(", with ");
                    sb.Append(scoring.CommonTags.Count);
                    sb.Append(scoring.CommonTags.Count == 1 ? " shared tag" : " shared tags");
                    sb.Append(" supporting the result");
                }

                sb.Append('.');
            }
            else if (scoring.CommonTags.Count > 0)
            {
                sb.Append(" It also shares ");
                sb.Append(scoring.CommonTags.Count);
                sb.Append(scoring.CommonTags.Count == 1 ? " tag" : " tags");
                sb.Append(" with your search, which supports the result.");
            }

            if (scoring.Penalty < 0)
            {
                sb.Append(" Note that the object types differ, which lowers the confidence of this match.");
            }

            return sb.ToString();
        }

        private static string BuildEnglishOpening(ScoringSideInfo scoring)
        {
            if (scoring.TextSimilarity >= Thresholds.High)
            {
                return "A highly similar report was found because the description closely matches your search.";
            }

            if (scoring.TextSimilarity >= Thresholds.Moderate)
            {
                return "A moderately similar report was found because the description overlaps with your search.";
            }

            if (scoring.ImageSimilarity >= Thresholds.High)
            {
                return "A highly similar report was found because the image closely matches your search.";
            }

            if (scoring.ImageSimilarity >= Thresholds.Moderate)
            {
                return "A moderately similar report was found because the image shows some similarity to your search.";
            }

            return "This report shows some overlap with your search, though the description and image are only loosely related.";
        }

        /// <summary>
        /// Joins clauses into a natural "a, b, and c" style list instead of a
        /// mechanical comma-separated dump.
        /// </summary>
        private static string JoinNaturally(List<string> clauses)
        {
            return clauses.Count switch
            {
                1 => clauses[0],
                2 => $"{clauses[0]} and {clauses[1]}",
                _ => string.Join(", ", clauses.GetRange(0, clauses.Count - 1)) + ", and " + clauses[^1]
            };
        }

        // =====================================================================
        // ---- Arabic -----------------------------------------------------------
        // =====================================================================

        private static string BuildArabic(ReasonSummary summary)
        {
            var scoring = summary.Scoring;
            var candidate = summary.Candidate;

            var sb = new StringBuilder(BuildArabicOpening(scoring));

            var attributeClauses = new List<string>(4);

            if (scoring.ObjectTypeMatched)
            {
                attributeClauses.Add(!string.IsNullOrWhiteSpace(candidate.ObjectType)
                    ? $"نوع المنتج متطابق ({candidate.ObjectType})"
                    : "نوع المنتج متطابق");
            }

            if (scoring.ColorMatched)
            {
                attributeClauses.Add(!string.IsNullOrWhiteSpace(candidate.Color)
                    ? $"اللون متطابق ({candidate.Color})"
                    : "اللون متطابق");
            }

            if (scoring.BrandMatched)
            {
                attributeClauses.Add(!string.IsNullOrWhiteSpace(candidate.Brand)
                    ? $"العلامة التجارية متطابقة ({candidate.Brand})"
                    : "العلامة التجارية متطابقة");
            }

            if (attributeClauses.Count > 0)
            {
                sb.Append(" كما أن ");
                sb.Append(string.Join("، و", attributeClauses));

                if (scoring.CommonTags.Count > 0)
                {
                    sb.Append("، مع وجود ");
                    sb.Append(scoring.CommonTags.Count);
                    sb.Append(scoring.CommonTags.Count == 1 ? " وسم مشترك" : " وسوم مشتركة");
                    sb.Append(" تدعم هذا التطابق");
                }

                sb.Append('.');
            }
            else if (scoring.CommonTags.Count > 0)
            {
                sb.Append(" كما يوجد ");
                sb.Append(scoring.CommonTags.Count);
                sb.Append(scoring.CommonTags.Count == 1 ? " وسم مشترك" : " وسوم مشتركة");
                sb.Append(" يدعم هذا التطابق.");
            }

            if (scoring.Penalty < 0)
            {
                sb.Append(" يُشار إلى أن نوع الكائن مختلف، مما يقلل من درجة الثقة في هذا التطابق.");
            }

            return sb.ToString();
        }

        private static string BuildArabicOpening(ScoringSideInfo scoring)
        {
            if (scoring.TextSimilarity >= Thresholds.High)
            {
                return "تم العثور على عنصر مشابه لبحثك لأن الوصف متقارب بشكل كبير.";
            }

            if (scoring.TextSimilarity >= Thresholds.Moderate)
            {
                return "تم العثور على عنصر مشابه إلى حد ما لأن الوصف متقارب جزئيًا مع بحثك.";
            }

            if (scoring.ImageSimilarity >= Thresholds.High)
            {
                return "تم العثور على عنصر مشابه لبحثك لأن الصورة متقاربة بشكل كبير.";
            }

            if (scoring.ImageSimilarity >= Thresholds.Moderate)
            {
                return "تم العثور على عنصر يظهر بعض التشابه مع الصورة التي بحثت عنها.";
            }

            return "هذا العنصر يظهر تشابهًا محدودًا مع بحثك، مع تداخل قليل بين العنصرين.";
        }
    }
}
