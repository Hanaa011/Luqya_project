using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Query
{
    // Rule-based classification over a small, closed, curated vocabulary -
    // generalizes the intent-word list the pre-Phase-2B SearchTextProcessor
    // had inline into a real, reusable, testable service.
    internal sealed class IntentDetector : IIntentDetector
    {
        private static readonly HashSet<string> LostWords = new(System.StringComparer.Ordinal)
        {
            "ضيعت", "أضعت", "اضعت", "ضاع", "ضاعت", "ضيع", "فقدت", "فقدان", "فقد",
            "lost", "lose", "losing", "misplaced"
        };

        private static readonly HashSet<string> FoundWords = new(System.StringComparer.Ordinal)
        {
            "وجدت", "لقيت", "لقيته", "عثرت", "وجد","حصلت",
            // "العثور" ("the finding") is the content-bearing word of the
            // common "تم العثور على" ("[it] was found") construction - added
            // so that phrase's tokens are excluded from
            // LocalClassificationProvider's object-extraction fallback the
            // same way single-word "لقيت"/"وجدت" already are. Deliberately
            // NOT adding "تم" (too short to ever pass that fallback's own
            // length filter) or "على" (a common, semantically unrelated
            // preposition - adding it here would falsely tag countless
            // unrelated queries, e.g. "...موجود على الطاولة", as FoundItem
            // intent via Detect()).
            "العثور",
            "found", "find", "finding"
        };

        private static readonly HashSet<string> QuestionWords = new(System.StringComparer.Ordinal)
        {
            "هل", "كيف", "لماذا", "متى", "أين", "اين",
            "what", "how", "why", "when", "where", "who", "is", "are", "can", "does", "do"
        };

        public IntentDetectionResult Detect(IReadOnlyList<string> tokens, string languageCode)
        {
            if (tokens.Any(LostWords.Contains))
            {
                return new IntentDetectionResult(QueryIntent.LostItem, 0.9);
            }

            if (tokens.Any(FoundWords.Contains))
            {
                return new IntentDetectionResult(QueryIntent.FoundItem, 0.9);
            }

            if (tokens.Count > 0 && QuestionWords.Contains(tokens[0]))
            {
                return new IntentDetectionResult(QueryIntent.GeneralQuestion, 0.6);
            }

            return tokens.Count > 0
                ? new IntentDetectionResult(QueryIntent.SearchRequest, 0.5)
                : new IntentDetectionResult(QueryIntent.Unknown, 0.0);
        }

        public bool IsIntentWord(string token) =>
            LostWords.Contains(token) || FoundWords.Contains(token) || QuestionWords.Contains(token);
    }
}
