using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Core
{
    // Capability abstraction introduced in Phase 2A Part 1 (Enterprise AI
    // Foundation): callers depend on "generate an embedding", never on WHICH
    // provider or runtime does it. Today this is implemented purely as a
    // resilience-decorated wrapper over the single configured
    // IEmbeddingProvider (see LostFound.AI.Embeddings.ProviderFallbackEmbeddingEngine
    // in LostFound.Application) - Phase 2A Part 2 introduces a local ONNX
    // runtime behind this same interface without any caller ever changing.
    public interface IEmbeddingEngine
    {
        // Name of the engine/provider actually serving requests right now -
        // surfaced to callers that need to record provenance (e.g.
        // MatchManager.FindAndCreateMatchesAsync's "matchedByProvider"
        // argument), without exposing the underlying provider type.
        string EngineName { get; }

        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        // See IEmbeddingProvider.GenerateImageEmbeddingAsync: caption-then-embed,
        // not a dedicated multimodal embedding model.
        Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    }
}
