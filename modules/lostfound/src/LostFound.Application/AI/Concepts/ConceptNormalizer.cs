using LostFound.AI.Languages;

namespace LostFound.AI.Concepts
{
    internal sealed class ConceptNormalizer(LanguageNormalizerRegistry registry) : IConceptNormalizer
    {
        public string Normalize(string text, string languageCode) => registry.Normalize(text, languageCode);
    }
}
