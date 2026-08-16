using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Concepts;

namespace LostFound.AI.Query
{
    /// <summary>
    /// Dictionary-assisted correction via Levenshtein edit distance against
    /// the known concept vocabulary (Phase 2A Part 3's canonical/localized
    /// names, synonyms, and aliases) - the spec's "Edit Distance" and
    /// "Dictionary assisted correction" techniques. "Keyboard proximity"
    /// and "Semantic correction" (the spec's other two named techniques)
    /// are NOT implemented: keyboard proximity needs a real per-layout
    /// adjacency table, and semantic correction needs the embedding engine
    /// comparing candidate meanings, not just edit distance - both are
    /// real, separate techniques worth adding later, not faked here.
    /// </summary>
    /// <remarks>
    /// The vocabulary is built lazily once and not automatically rebuilt
    /// after a later dataset import within the same process lifetime -
    /// unlike InMemoryAliasResolver (Phase 2A Part 3), this class has no
    /// RebuildIndexAsync hook in its interface yet. A known, documented
    /// limitation rather than a silent one.
    ///
    /// PHASE-VALIDATION-07: the known-word vocabulary built here MUST use
    /// the same per-language character normalization
    /// (<see cref="IConceptNormalizer"/>) that runtime tokens already went
    /// through by the time <see cref="CorrectAsync"/> sees them (QueryPipeline
    /// normalizes via ITextNormalizer before tokenization, which runs before
    /// spell correction). Before this fix, an already-normalized Arabic
    /// token like "ابيض" (hamza collapsed) was never found in a vocabulary
    /// still keyed by the un-normalized ontology spelling "أبيض", so the
    /// Levenshtein matcher below "corrected" the token right back to the
    /// un-normalized form - silently undoing normalization and reintroducing
    /// the exact mismatch EntityRecognizer's own vocabulary build had to be
    /// fixed for. The two fixes are not independent: fixing only one of
    /// them (this service or EntityRecognizer) would have broken whichever
    /// concept happened to rely on the two being equally wrong.
    /// </remarks>
    internal sealed class DictionarySpellCorrectionService(
        IConceptRepository conceptRepository, IConceptNormalizer conceptNormalizer) : ISpellCorrectionService
    {
        private const int MaxEditDistance = 2;

        private readonly SemaphoreSlim _buildLock = new(1, 1);
        private Dictionary<string, HashSet<string>>? _vocabularyByLanguage;

        public async Task<IReadOnlyList<SpellCorrection>> CorrectAsync(
            IReadOnlyList<string> tokens, string languageCode, CancellationToken cancellationToken = default)
        {
            var vocabulary = await EnsureVocabularyAsync(cancellationToken);

            if (!vocabulary.TryGetValue(languageCode, out var knownWords) || knownWords.Count == 0)
            {
                return Array.Empty<SpellCorrection>();
            }

            var corrections = new List<SpellCorrection>();

            foreach (var token in tokens)
            {
                if (token.Length < 3 || knownWords.Contains(token))
                {
                    continue; // too short to correct reliably, or already a known word
                }

                var (bestMatch, bestDistance) = FindClosestMatch(token, knownWords);

                // PHASE-VALIDATION-08: a flat "distance <= 2" accepts a
                // 50% character change on a short word (e.g. "lost", 4
                // chars) as a confident correction - real, reproduced
                // impact: growing the ontology vocabulary (this phase's
                // whole point) increased the pool of candidate correction
                // targets enough that the common English word "lost" itself
                // started getting "corrected" to an unrelated nearby
                // vocabulary word, corrupting IntentDetector (which matches
                // "lost" literally) downstream. The acceptance bar now
                // scales with word length - short words need a
                // proportionally closer match - while long words keep the
                // original, more generous distance-2 allowance. This is a
                // general precision fix, not a word-specific exclusion list.
                var maxAllowedDistance = Math.Min(MaxEditDistance, token.Length / 3);
                if (bestMatch != null && bestDistance <= maxAllowedDistance && bestDistance < token.Length)
                {
                    var confidence = 1.0 - (double)bestDistance / Math.Max(token.Length, bestMatch.Length);
                    corrections.Add(new SpellCorrection(token, bestMatch, confidence));
                }
            }

            return corrections;
        }

        private async Task<Dictionary<string, HashSet<string>>> EnsureVocabularyAsync(CancellationToken cancellationToken)
        {
            if (_vocabularyByLanguage != null)
            {
                return _vocabularyByLanguage;
            }

            await _buildLock.WaitAsync(cancellationToken);
            try
            {
                if (_vocabularyByLanguage != null)
                {
                    return _vocabularyByLanguage;
                }

                var vocabulary = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                var concepts = await conceptRepository.GetAllAsync(cancellationToken);

                void AddWord(string language, string word)
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        return;
                    }

                    if (!vocabulary.TryGetValue(language, out var set))
                    {
                        set = new HashSet<string>(StringComparer.Ordinal);
                        vocabulary[language] = set;
                    }

                    // Must match the same per-language normalization runtime
                    // tokens already went through (see class remarks) -
                    // otherwise this "known words" set never contains the
                    // form a genuinely-correct token actually arrives in.
                    set.Add(conceptNormalizer.Normalize(word, language).Trim().ToLowerInvariant());
                }

                foreach (var concept in concepts)
                {
                    foreach (var (language, name) in concept.LocalizedNames)
                    {
                        AddWord(language, name);
                    }

                    foreach (var (language, words) in concept.Synonyms)
                    {
                        foreach (var word in words)
                        {
                            AddWord(language, word);
                        }
                    }

                    foreach (var (language, words) in concept.Aliases)
                    {
                        foreach (var word in words)
                        {
                            AddWord(language, word);
                        }
                    }
                }

                _vocabularyByLanguage = vocabulary;
                return vocabulary;
            }
            finally
            {
                _buildLock.Release();
            }
        }

        private static (string? Match, int Distance) FindClosestMatch(string token, HashSet<string> knownWords)
        {
            string? best = null;
            var bestDistance = int.MaxValue;

            foreach (var word in knownWords)
            {
                if (Math.Abs(word.Length - token.Length) > MaxEditDistance)
                {
                    continue; // cheap pre-filter before computing the real distance
                }

                var distance = TextSimilarity.LevenshteinDistance(token, word);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = word;
                }
            }

            return (best, bestDistance);
        }
    }
}
