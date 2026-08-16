using System.Collections.Generic;

namespace LostFound.AI.Query
{
    public sealed record IntentDetectionResult(QueryIntent Intent, double Confidence);

    // Rule-based classification (the same intent-word list the pre-Phase-2B
    // SearchTextProcessor used, generalized into a real service) - lost/found
    // report intent is a small, closed vocabulary in every supported
    // language, which is exactly the case rule-based classification handles
    // well without needing a trained model.
    public interface IIntentDetector
    {
        IntentDetectionResult Detect(IReadOnlyList<string> tokens, string languageCode);

        // Exposes the same curated Lost/Found/Question vocabulary Detect()
        // itself is built from, for callers that need to ask "is THIS one
        // token an intent/action/question word" rather than classify a
        // whole query - see LocalClassificationProvider's object-extraction
        // fallback, which must never select an intent word as the item
        // being reported.
        bool IsIntentWord(string token);
    }
}
