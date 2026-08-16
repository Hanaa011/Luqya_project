using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Runtime
{
    // A single loaded local embedding model instance (weights + tokenizer),
    // owned and lifecycle-managed by an IEmbeddingRuntime implementation.
    // Kept as its own abstraction (rather than folding EmbedAsync directly
    // into IEmbeddingRuntime) so the runtime is free to swap/reload models
    // (upgrade, rollback) without callers of IEmbeddingRuntime ever seeing
    // model-level detail.
    public interface IEmbeddingModel : IDisposable
    {
        string Name { get; }
        string Version { get; }
        int Dimensions { get; }
        IReadOnlyList<string> SupportedLanguages { get; }

        Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    }
}
