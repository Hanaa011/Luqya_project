using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Core;

namespace LostFound.AI.Embeddings
{
    /// <summary>
    /// Default <see cref="IEmbeddingEngine"/>: forwards to whichever single
    /// <see cref="IEmbeddingProvider"/> is registered (see
    /// <see cref="LostFoundAiProvidersServiceCollectionExtensions.AddLostFoundSemanticAiFoundation"/>),
    /// possibly already resilience-wrapped by
    /// <see cref="Providers.ResilientEmbeddingProvider"/>.
    /// </summary>
    /// <remarks>
    /// Named "Fallback" because this is the seam a future phase (local ONNX
    /// runtime as primary, external provider as the fallback enhancement -
    /// see docs/Semantic-AI/Phase-2A/PHASE-2A-PART-2-Local-AI-Runtime.md)
    /// will extend to actually try more than one source. Phase 2A Part 1's
    /// own exit criteria require zero behavior change, so today it wraps
    /// exactly one provider, identical to the pre-Part-1 behavior - real
    /// multi-source fallback is intentionally deferred until there is a
    /// second, genuinely independent source (the local runtime) to fall back
    /// to/from.
    /// </remarks>
    internal sealed class ProviderFallbackEmbeddingEngine(IEmbeddingProvider provider) : IEmbeddingEngine
    {
        public string EngineName => provider.ProviderName;

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            provider.GenerateEmbeddingAsync(text, cancellationToken);

        public Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default) =>
            provider.GenerateImageEmbeddingAsync(imageBytes, cancellationToken);
    }
}
