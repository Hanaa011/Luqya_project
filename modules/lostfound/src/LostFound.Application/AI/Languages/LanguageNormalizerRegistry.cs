using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Languages
{
    // Dictionary-keyed registry (mirrors LostFound.AI.Providers.AiProviderRegistry
    // from Phase 2A Part 1) so ConceptNormalizer never switches on language
    // code - adding a language is registering one more ILanguageNormalizer,
    // never touching this class or ConceptNormalizer.
    internal sealed class LanguageNormalizerRegistry
    {
        private readonly IReadOnlyDictionary<string, ILanguageNormalizer> _normalizers;

        public LanguageNormalizerRegistry(IEnumerable<ILanguageNormalizer> normalizers)
        {
            _normalizers = normalizers.ToDictionary(n => n.LanguageCode, System.StringComparer.OrdinalIgnoreCase);
        }

        // Falls back to a no-op identity normalizer for an unregistered
        // language rather than throwing - an unsupported language should
        // degrade to "compare as-is", not break concept resolution entirely.
        public string Normalize(string text, string languageCode) =>
            _normalizers.TryGetValue(languageCode, out var normalizer) ? normalizer.Normalize(text) : text.Trim();
    }
}
