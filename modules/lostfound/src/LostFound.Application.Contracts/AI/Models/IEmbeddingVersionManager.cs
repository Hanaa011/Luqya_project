using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Models
{
    // Persisted installation history for embedding models - which versions
    // were ever installed, and which one is currently active. Kept separate
    // from IEmbeddingModelManager so "record what happened" (a storage
    // concern) stays independent of "decide what should happen"
    // (orchestration) - see EmbeddingModelManager, which implements both.
    public interface IEmbeddingVersionManager
    {
        Task RecordInstalledAsync(EmbeddingModelInfo info, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<EmbeddingModelInfo>> GetHistoryAsync(string name, CancellationToken cancellationToken = default);

        Task<EmbeddingModelInfo?> GetActiveAsync(CancellationToken cancellationToken = default);

        Task SetActiveAsync(string name, string version, CancellationToken cancellationToken = default);
    }
}
