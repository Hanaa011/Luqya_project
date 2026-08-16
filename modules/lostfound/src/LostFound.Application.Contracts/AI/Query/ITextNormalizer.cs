namespace LostFound.AI.Query
{
    // The pipeline's "Unicode Normalization" stage - script-level, language-
    // AGNOSTIC cleanup (Unicode NFC form, Arabic letter-form collapsing,
    // punctuation stripping) applied before language-specific rules
    // (LostFound.AI.Languages.ILanguageNormalizer, Phase 2A Part 3) run.
    // Distinct from ILanguageNormalizer: this runs on every query regardless
    // of detected language (mixed-script queries are common - "gold ايفون" -
    // and both halves need this pass).
    public interface ITextNormalizer
    {
        string Normalize(string text);
    }
}
