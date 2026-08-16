namespace LostFound.AI.Concepts
{
    // Normalizes raw text for concept matching, dispatching to the
    // language-specific rules in LostFound.AI.Languages. Kept as its own
    // interface (rather than folded into IConceptResolver) so normalization
    // - a pure, synchronous text transform - can be unit tested and reused
    // (e.g. by dataset importers canonicalizing incoming data) independent
    // of the async repository/resolver layer.
    public interface IConceptNormalizer
    {
        string Normalize(string text, string languageCode);
    }
}
