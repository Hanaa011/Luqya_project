using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Runtime;

namespace LostFound.AI.Diagnostics
{
    public sealed record EmbeddingRuntimeDiagnosticsReport(
        EmbeddingRuntimeStatus RuntimeStatus,
        int CacheEntryCount,
        long AverageInferenceLatencyMs,
        long LastInferenceLatencyMs);

    // Aggregates runtime/model/cache health into one report - the seam a
    // future ASP.NET Core IHealthCheck (or an admin diagnostics endpoint)
    // can be built on without reaching into the runtime/cache internals
    // directly.
    public interface IEmbeddingRuntimeDiagnostics
    {
        Task<EmbeddingRuntimeDiagnosticsReport> GetReportAsync(CancellationToken cancellationToken = default);

        void RecordInferenceLatency(long elapsedMilliseconds);
    }
}
