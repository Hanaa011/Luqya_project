using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.AI.Storage;

namespace LostFound.AI.Caching
{
    // Bounded in-process cache with simple FIFO eviction once
    // MemoryCacheMaxEntries is exceeded. Not LRU - a full LRU would need a
    // lock-guarded linked list and this cache sits in the hot path of every
    // search; FIFO is a deliberately cheap, lock-free approximation that
    // still bounds memory, which is all this layer needs to guarantee
    // (IEmbeddingStore is the durable, unbounded layer underneath it).
    //
    // Also declared as ICacheStore - see Storage/StorageAbstractions.cs for
    // why that's a marker interface rather than a second implementation.
    internal sealed class MemoryEmbeddingCache : IEmbeddingCache, ICacheStore
    {
        private readonly ConcurrentDictionary<string, float[]> _entries = new();
        private readonly ConcurrentQueue<string> _insertionOrder = new();
        private readonly int _maxEntries;

        public MemoryEmbeddingCache(IOptions<LocalAiRuntimeOptions> options)
        {
            _maxEntries = options.Value.MemoryCacheMaxEntries;
        }

        public int Count => _entries.Count;

        public bool TryGet(string cacheKey, out float[]? embedding) => _entries.TryGetValue(cacheKey, out embedding);

        public void Set(string cacheKey, float[] embedding)
        {
            if (_entries.TryAdd(cacheKey, embedding))
            {
                _insertionOrder.Enqueue(cacheKey);
                EvictIfNeeded();
            }
            else
            {
                _entries[cacheKey] = embedding;
            }
        }

        private void EvictIfNeeded()
        {
            while (_entries.Count > _maxEntries && _insertionOrder.TryDequeue(out var oldestKey))
            {
                _entries.TryRemove(oldestKey, out _);
            }
        }
    }
}
