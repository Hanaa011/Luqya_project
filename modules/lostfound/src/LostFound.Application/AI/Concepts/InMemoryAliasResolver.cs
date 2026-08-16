using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Concepts
{
    // Lazily builds an in-memory (normalizedText, language) -> ConceptId
    // index from IConceptRepository on first use, per Phase 1's storage
    // decision ("a memory-mapped/serialized in-memory index built from it
    // [SQLite] at startup for hot-path lookups"). Indexes canonical names,
    // localized names, synonyms, aliases, dialect words, and misspellings -
    // every text form a user might actually type - all pointing at the same
    // ConceptId. Must be rebuilt (RebuildIndexAsync) after bulk writes, most
    // importantly Phase 2A Part 4's dataset importers.
    internal sealed class InMemoryAliasResolver(IConceptRepository conceptRepository, IConceptNormalizer normalizer, ILogger<InMemoryAliasResolver> logger) : IAliasResolver
    {
        private readonly SemaphoreSlim _buildLock = new(1, 1);
        private ConcurrentDictionary<(string Text, string Language), Guid>? _index;

        public async Task<Guid?> ResolveAsync(string normalizedText, string languageCode, CancellationToken cancellationToken = default)
        {
            var index = await EnsureIndexAsync(cancellationToken);
            return index.TryGetValue((normalizedText, languageCode.ToLowerInvariant()), out var conceptId) ? conceptId : null;
        }

        public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
        {
            await _buildLock.WaitAsync(cancellationToken);
            try
            {
                _index = await BuildIndexAsync(cancellationToken);
                logger.LogInformation("Alias index rebuilt with {Count} entries.", _index.Count);
            }
            finally
            {
                _buildLock.Release();
            }
        }

        private async Task<ConcurrentDictionary<(string, string), Guid>> EnsureIndexAsync(CancellationToken cancellationToken)
        {
            if (_index != null)
            {
                return _index;
            }

            await _buildLock.WaitAsync(cancellationToken);
            try
            {
                _index ??= await BuildIndexAsync(cancellationToken);
                return _index;
            }
            finally
            {
                _buildLock.Release();
            }
        }

        private async Task<ConcurrentDictionary<(string, string), Guid>> BuildIndexAsync(CancellationToken cancellationToken)
        {
            var index = new ConcurrentDictionary<(string, string), Guid>();
            var concepts = await conceptRepository.GetAllAsync(cancellationToken);

            foreach (var concept in concepts)
            {
                foreach (var language in concept.LanguageAvailability)
                {
                    IndexIfPresent(index, normalizer.Normalize(concept.CanonicalName, language), language, concept.Id);
                }

                IndexLocalized(index, concept.LocalizedNames, concept.Id);
                IndexWordLists(index, concept.Synonyms, concept.Id);
                IndexWordLists(index, concept.Aliases, concept.Id);
                IndexWordLists(index, concept.DialectWords, concept.Id);
                IndexWordLists(index, concept.CommonMisspellings, concept.Id);
            }

            return index;
        }

        private void IndexLocalized(ConcurrentDictionary<(string, string), Guid> index, IReadOnlyDictionary<string, string> localizedNames, Guid conceptId)
        {
            foreach (var (language, name) in localizedNames)
            {
                IndexIfPresent(index, normalizer.Normalize(name, language), language, conceptId);
            }
        }

        private void IndexWordLists(
            ConcurrentDictionary<(string, string), Guid> index,
            IReadOnlyDictionary<string, IReadOnlyList<string>> wordsByLanguage,
            Guid conceptId)
        {
            foreach (var (language, words) in wordsByLanguage)
            {
                foreach (var word in words)
                {
                    IndexIfPresent(index, normalizer.Normalize(word, language), language, conceptId);
                }
            }
        }

        private static void IndexIfPresent(ConcurrentDictionary<(string, string), Guid> index, string normalizedText, string language, Guid conceptId)
        {
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return;
            }

            // First concept to claim a given text wins - a genuine ambiguous
            // collision (two concepts sharing an alias) is a data-quality
            // issue for the dataset importer/curation step to resolve, not
            // something this hot-path lookup should silently overwrite on
            // every rebuild.
            index.TryAdd((normalizedText, language.ToLowerInvariant()), conceptId);
        }
    }
}
