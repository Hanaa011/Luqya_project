using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Models
{
    // Orchestrates the full model lifecycle: install (download + verify),
    // list what's installed, activate a version, and roll back. Composes
    // IEmbeddingDownloader (fetch + checksum) and IEmbeddingVersionManager
    // (persisted history) rather than duplicating either concern.
    public interface IEmbeddingModelManager
    {
        Task<EmbeddingModelInfo?> GetActiveModelAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<EmbeddingModelInfo>> GetInstalledModelsAsync(CancellationToken cancellationToken = default);

        Task<EmbeddingModelInfo> InstallAsync(EmbeddingModelDescriptor descriptor, CancellationToken cancellationToken = default);

        Task ActivateAsync(string name, string version, CancellationToken cancellationToken = default);

        Task RollbackToPreviousAsync(CancellationToken cancellationToken = default);

        // Recomputes the installed file's checksum and compares it to the
        // recorded one - used both right after install and by diagnostics
        // to detect on-disk corruption/tampering after the fact.
        Task<bool> VerifyIntegrityAsync(string name, string version, CancellationToken cancellationToken = default);
    }
}
