using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.AI.Concepts;
using LostFound.AI.Storage;

namespace LostFound.AI.Caching
{
    // Same bounded-FIFO-eviction shape as MemoryEmbeddingCache (Phase 2A
    // Part 2) - a lock-free, cheap approximation of LRU that's good enough
    // for a hot-path cache sitting in front of every concept lookup.
    //
    // Also declared as ICacheStore - see Storage/StorageAbstractions.cs for
    // why that's a marker interface rather than a second implementation.
    internal sealed class MemoryConceptCache : IConceptCache, ICacheStore
    {
        private readonly ConcurrentDictionary<Guid, Concept> _entries = new();
        private readonly ConcurrentQueue<Guid> _insertionOrder = new();
        private readonly int _maxEntries;

        public MemoryConceptCache(IOptions<KnowledgeGraphOptions> options)
        {
            _maxEntries = options.Value.ConceptCacheMaxEntries;
        }

        public int Count => _entries.Count;

        public bool TryGet(Guid conceptId, out Concept? concept) => _entries.TryGetValue(conceptId, out concept);

        public void Set(Guid conceptId, Concept concept)
        {
            if (_entries.TryAdd(conceptId, concept))
            {
                _insertionOrder.Enqueue(conceptId);
                EvictIfNeeded();
            }
            else
            {
                _entries[conceptId] = concept;
            }
        }

        public void Invalidate(Guid conceptId) => _entries.TryRemove(conceptId, out _);

        private void EvictIfNeeded()
        {
            while (_entries.Count > _maxEntries && _insertionOrder.TryDequeue(out var oldestKey))
            {
                _entries.TryRemove(oldestKey, out _);
            }
        }
    }
}
