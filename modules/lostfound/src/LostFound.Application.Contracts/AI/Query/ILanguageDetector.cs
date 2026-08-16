namespace LostFound.AI.Query
{
    public sealed record LanguageDetectionResult(string LanguageCode, double Confidence);

    // Script/character-range heuristic detection - genuinely reliable for
    // Arabic-script-vs-Latin-script, honestly approximate for Arabic-vs-Urdu
    // (both share the Arabic script; disambiguation relies on Urdu-specific
    // characters like ٹ/ڈ/ڑ/ں/ے being present, which isn't guaranteed for
    // short queries) - see HeuristicLanguageDetector's own remarks.
    public interface ILanguageDetector
    {
        LanguageDetectionResult Detect(string text);
    }
}
