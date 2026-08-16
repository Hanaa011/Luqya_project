using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Caching;
using LostFound.AI.Core;
using LostFound.AI.Diagnostics;
using LostFound.AI.Runtime;
using LostFound.AI.Storage;

namespace LostFound.AI.Embeddings
{
    // The Phase 2A Part 2 default IEmbeddingEngine: tries the local ONNX
    // runtime first (per the spec's "All embeddings must be generated
    // locally" / "External AI providers become optional" mandate), falling
    // back to the Part 1 provider-based engine whenever the local runtime
    // isn't available or fails. Registered by
    // LostFoundLocalAiRuntimeServiceCollectionExtensions.AddLostFoundLocalAiRuntime,
    // which overrides Part 1's plain provider-fallback registration - see
    // that file for why this doesn't create a circular DI resolution.
    //
    // "Search must never fail" (Phase 1 Part 1, Principle 3) is upheld here:
    // any local-runtime failure - not installed, load failure, or an
    // inference-time exception - degrades to the external provider rather
    // than throwing out to the caller.
    internal sealed class LocalFirstEmbeddingEngine : IEmbeddingEngine
    {
        private readonly IEmbeddingRuntime _runtime;
        private readonly IEmbeddingEngine _fallback;
        private readonly IEmbeddingCache _cache;
        private readonly IEmbeddingStore _store;
        private readonly IEmbeddingRuntimeDiagnostics _diagnostics;
        private readonly ILogger<LocalFirstEmbeddingEngine> _logger;

        public LocalFirstEmbeddingEngine(
            IEmbeddingRuntime runtime,
            IEmbeddingEngine fallback,
            IEmbeddingCache cache,
            IEmbeddingStore store,
            IEmbeddingRuntimeDiagnostics diagnostics,
            ILogger<LocalFirstEmbeddingEngine> logger)
        {
            _runtime = runtime;
            _fallback = fallback;
            _cache = cache;
            _store = store;
            _diagnostics = diagnostics;
            _logger = logger;
        }

        public string EngineName => _runtime.IsAvailable
            ? $"Local:{_runtime.ActiveModelVersion}"
            : _fallback.EngineName;

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            // IEmbeddingRuntime.IsAvailable is a plain snapshot of the last
            // load attempt - it does not itself trigger one (see
            // OnnxEmbeddingRuntime.EnsureModelLoadedAsync, only reached via
            // GetStatusAsync/GenerateEmbeddingAsync). Reading it directly
            // here, before ever calling into the runtime, meant the local
            // model could never load on a fresh process: IsAvailable starts
            // false, so this method always took the fallback branch below
            // without ever making the one call that would have made
            // IsAvailable true. GetStatusAsync forces that lazy-load attempt
            // (a cheap no-op once a model is already loaded) so IsAvailable
            // reflects reality before it's used to decide anything.
            await _runtime.GetStatusAsync(cancellationToken);

            if (!_runtime.IsAvailable)
            {
                return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
            }

            var modelVersion = _runtime.ActiveModelVersion!;
            var cacheKey = ComputeCacheKey(text);

            if (_cache.TryGet(cacheKey, out var cached) && cached != null)
            {
                return cached;
            }

            var stored = await _store.TryGetAsync(cacheKey, modelVersion, cancellationToken);
            if (stored != null)
            {
                _cache.Set(cacheKey, stored);
                return stored;
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var embedding = await _runtime.GenerateEmbeddingAsync(text, cancellationToken);
                _diagnostics.RecordInferenceLatency(stopwatch.ElapsedMilliseconds);

                _cache.Set(cacheKey, embedding);
                await _store.SaveAsync(cacheKey, modelVersion, embedding, cancellationToken);

                return embedding;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Local embedding runtime failed; falling back to the external provider.");
                return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
            }
        }

        // Local runtime is text-only (Part 2 spec: "multilingual embeddings",
        // no vision model) - images still go through the caption-then-embed
        // external provider path from Phase 2A Part 1.
        public Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default) =>
            _fallback.GenerateImageEmbeddingAsync(imageBytes, cancellationToken);

        private static string ComputeCacheKey(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
