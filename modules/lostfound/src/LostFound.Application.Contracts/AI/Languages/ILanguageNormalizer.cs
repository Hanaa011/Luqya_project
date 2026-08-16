namespace LostFound.AI.Languages
{
    // One language's normalization rules (diacritics, casing, lemmatization,
    // etc. - see the Part 3 spec's "Language Normalization" section for the
    // exact per-language rule list). Implementations are looked up by
    // LanguageCode through a registry (LostFound.AI.Languages.LanguageNormalizerRegistry)
    // rather than a switch statement, so adding a language (Phase 1's
    // "Future" list: Hindi, Turkish, Persian, Malay, French) never requires
    // touching existing code - it's a new class + one registry entry.
    public interface ILanguageNormalizer
    {
        string LanguageCode { get; }

        string Normalize(string text);
    }
}
