using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.AI.Core;
using LostFound.AI.Importers;

namespace LostFound.AI.Query
{
    // Cache key includes normalized query + language + embedding engine
    // name (proxy for "model version" - LocalFirstEmbeddingEngine.EngineName
    // already encodes the active local model's version, or the external
    // provider's name when no local model is installed) + a "knowledge
    // version" computed from every registered IDatasetImporter's latest
    // successfully-imported DatasetVersion (Phase 2A Part 4/5) - so a cached
    // SemanticQuery is automatically invalidated the moment either the
    // active model or the underlying knowledge graph changes, per the spec.
    internal sealed class MemoryQueryCache(
        IEmbeddingEngine embeddingEngine,
        IDatasetImportHistoryRepository importHistory,
        IEnumerable<IDatasetImporter> importers,
        IOptions<QueryPipelineOptions> options) : IQueryCache
    {
        private readonly ConcurrentDictionary<string, SemanticQuery> _entries = new();
        private readonly ConcurrentQueue<string> _insertionOrder = new();
        private readonly int _maxEntries = options.Value.MaxCacheEntries;

        public bool TryGet(string cacheKey, out SemanticQuery? query) => _entries.TryGetValue(cacheKey, out query);

        public void Set(string cacheKey, SemanticQuery query)
        {
            if (_entries.TryAdd(cacheKey, query))
            {
                _insertionOrder.Enqueue(cacheKey);
                while (_entries.Count > _maxEntries && _insertionOrder.TryDequeue(out var oldestKey))
                {
                    _entries.TryRemove(oldestKey, out _);
                }
            }
            else
            {
                _entries[cacheKey] = query;
            }
        }

        public async Task<string> BuildCacheKeyAsync(string normalizedText, string languageCode, CancellationToken cancellationToken = default)
        {
            var modelVersion = embeddingEngine.EngineName;
            var knowledgeVersion = await ComputeKnowledgeVersionAsync(cancellationToken);

            var raw = $"{normalizedText}|{languageCode}|{modelVersion}|{knowledgeVersion}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        private async Task<string> ComputeKnowledgeVersionAsync(CancellationToken cancellationToken)
        {
            var parts = new List<string>();

            foreach (var importer in importers)
            {
                var latest = await importHistory.GetLatestSuccessfulAsync(importer.DatasetName, cancellationToken);
                parts.Add(latest != null ? $"{importer.DatasetName}:{latest.DatasetVersion}" : $"{importer.DatasetName}:none");
            }

            return string.Join(",", parts.OrderBy(p => p, StringComparer.Ordinal));
        }
    }
}
