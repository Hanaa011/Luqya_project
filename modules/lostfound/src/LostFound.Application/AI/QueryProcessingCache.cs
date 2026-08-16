using System;
using System.Collections.Concurrent;

namespace LostFound.AI
{
    /// <summary>
    /// Process-wide cache for the two expensive-ish steps of turning a raw
    /// search string into a query embedding: text normalization/synonym
    /// expansion (cheap CPU, but still non-trivial per call) and the actual
    /// embedding API call (a real network round trip). <see cref="AiMatchingService"/>
    /// is registered <c>ITransientDependency</c> (a new instance per request),
    /// so these caches have to live outside it to actually be shared across
    /// requests - hence <c>static</c> here.
    ///
    /// Deliberately simple: an unbounded-looking <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// with a hard cap that just clears the whole cache once exceeded, rather
    /// than a real LRU. Search text has very high repeat-rate in practice
    /// (the same few dozen phrasings account for most traffic), so even this
    /// simple policy captures most of the benefit; swap in
    /// <c>IMemoryCache</c>/a proper LRU later if profiling says it's worth it.
    /// </summary>
    internal static class QueryProcessingCache
    {
        private const int MaxEntries = 500;

        private static readonly ConcurrentDictionary<string, string> NormalizedTextCache = new();
        private static readonly ConcurrentDictionary<string, float[]> EmbeddingCache = new();

        public static string GetOrAddNormalizedText(string rawText, Func<string, string> factory)
        {
            if (NormalizedTextCache.Count > MaxEntries)
            {
                NormalizedTextCache.Clear();
            }

            return NormalizedTextCache.GetOrAdd(rawText, factory);
        }

        public static bool TryGetEmbedding(string semanticText, out float[]? embedding) =>
            EmbeddingCache.TryGetValue(semanticText, out embedding);

        public static void SetEmbedding(string semanticText, float[] embedding)
        {
            if (EmbeddingCache.Count > MaxEntries)
            {
                EmbeddingCache.Clear();
            }

            EmbeddingCache[semanticText] = embedding;
        }
    }
}
