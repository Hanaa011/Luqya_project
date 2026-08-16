using LostFound.AI.Languages;

namespace LostFound.AI.Query
{
    // Reuses Phase 2A Part 3's per-language normalizers rather than
    // duplicating morphology logic - EnglishLanguageNormalizer already
    // implements the verifiably-correct mechanical subset (regular plural
    // stripping); Arabic/Urdu normalization here is character-form only
    // (no real morphological analyzer for either language exists in this
    // workspace - see EnglishLanguageNormalizer's own remarks on why full
    // lemmatization isn't faked).
    internal sealed class MorphologyService(LanguageNormalizerRegistry registry) : IMorphologyService
    {
        public string Lemmatize(string word, string languageCode) => registry.Normalize(word, languageCode);
    }
}
