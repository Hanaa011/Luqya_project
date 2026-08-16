using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Importers;

namespace LostFound.AI.Diagnostics
{
    public sealed record StorageHealth(string StoreName, bool FileExists, long? FileSizeBytes, DateTime? LastWriteTimeUtc);

    public sealed record AiPlatformDiagnosticsReport(
        EmbeddingRuntimeDiagnosticsReport EmbeddingRuntime,
        int ConceptCacheEntryCount,
        IReadOnlyList<StorageHealth> Storage,
        IReadOnlyList<DatasetImportRecord> LatestSuccessfulImports);

    // Aggregates health across every AI subsystem built in Phase 2A Parts
    // 1-5 into one report - the "Diagnostics & Observability" deliverable's
    // single entry point, and the seam a future ASP.NET Core IHealthCheck or
    // admin dashboard endpoint would be built on. Deliberately read-only and
    // side-effect-free: calling this never changes system state.
    public interface IAiPlatformDiagnostics
    {
        Task<AiPlatformDiagnosticsReport> GetReportAsync(CancellationToken cancellationToken = default);
    }
}
