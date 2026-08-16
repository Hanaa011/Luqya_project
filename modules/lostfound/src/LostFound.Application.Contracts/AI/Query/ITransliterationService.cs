namespace LostFound.AI.Query
{
    // Approximate Arabic <-> Latin phonetic transliteration - a fixed
    // character-mapping table (a real, standard technique for basic
    // matching), not a trained model. Genuinely useful for catching an
    // Arabic-script query matching a Latin-spelled brand name (or vice
    // versa) but not linguistically precise - see TransliterationService's
    // remarks for exactly what's covered.
    public interface ITransliterationService
    {
        string Transliterate(string text, string sourceLanguageCode);
    }
}
