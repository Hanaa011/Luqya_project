using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Runtime
{
    public enum EmbeddingRuntimeHealth
    {
        // No local model is installed/active yet - expected, normal state
        // until an operator installs one via IEmbeddingModelManager.
        NotInstalled,
        Loading,
        Healthy,
        Faulted
    }

    // Snapshot of the local runtime's current state, surfaced through
    // IEmbeddingRuntimeDiagnostics for health checks/ops visibility.
    public sealed record EmbeddingRuntimeStatus(
        EmbeddingRuntimeHealth Health,
        string? ActiveModelName,
        string? ActiveModelVersion,
        string? Detail);

    // Local, offline ONNX-based embedding runtime - the Phase 2A Part 2
    // counterpart to the external IEmbeddingProvider chain from Part 1.
    // Never called directly by application code; LostFound.AI.Embeddings.LocalFirstEmbeddingEngine
    // (which implements the stable IEmbeddingEngine capability from Part 1)
    // is the only consumer, so business logic never depends on this
    // interface or knows whether embeddings came from a local model or an
    // external provider.
    public interface IEmbeddingRuntime
    {
        // False whenever no model is installed/loaded or the last load
        // attempt failed - callers must treat this as the normal "not ready
        // yet, use the fallback" signal, not an error.
        bool IsAvailable { get; }

        string? ActiveModelVersion { get; }

        Task<EmbeddingRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default);

        // Throws if !IsAvailable - callers (LocalFirstEmbeddingEngine) must
        // check IsAvailable first and fall back rather than calling this.
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }
}
