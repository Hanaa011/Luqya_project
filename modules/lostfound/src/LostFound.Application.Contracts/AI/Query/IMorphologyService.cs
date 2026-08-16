namespace LostFound.AI.Query
{
    // Lemmatization/morphology - reduces a word to its base form (plurals,
    // simple inflection). Deliberately the same "mechanical subset only, not
    // faked" posture as Phase 2A Part 3's EnglishLanguageNormalizer: correct
    // irregular-form lemmatization needs a real dictionary this workspace
    // doesn't have, so this only handles what's verifiably correct.
    public interface IMorphologyService
    {
        string Lemmatize(string word, string languageCode);
    }
}
