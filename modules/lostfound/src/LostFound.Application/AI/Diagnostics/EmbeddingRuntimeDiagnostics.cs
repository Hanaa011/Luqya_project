using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Caching;
using LostFound.AI.Runtime;

namespace LostFound.AI.Diagnostics
{
    internal sealed class EmbeddingRuntimeDiagnostics : IEmbeddingRuntimeDiagnostics
    {
        private readonly IEmbeddingRuntime _runtime;
        private readonly IEmbeddingCache _cache;

        private long _latencySampleCount;
        private long _latencySampleSumMs;
        private long _lastLatencyMs;

        public EmbeddingRuntimeDiagnostics(IEmbeddingRuntime runtime, IEmbeddingCache cache)
        {
            _runtime = runtime;
            _cache = cache;
        }

        public void RecordInferenceLatency(long elapsedMilliseconds)
        {
            Interlocked.Exchange(ref _lastLatencyMs, elapsedMilliseconds);
            Interlocked.Increment(ref _latencySampleCount);
            Interlocked.Add(ref _latencySampleSumMs, elapsedMilliseconds);
        }

        public async Task<EmbeddingRuntimeDiagnosticsReport> GetReportAsync(CancellationToken cancellationToken = default)
        {
            var status = await _runtime.GetStatusAsync(cancellationToken);

            var count = Interlocked.Read(ref _latencySampleCount);
            var averageMs = count == 0 ? 0 : Interlocked.Read(ref _latencySampleSumMs) / count;

            return new EmbeddingRuntimeDiagnosticsReport(status, _cache.Count, averageMs, Interlocked.Read(ref _lastLatencyMs));
        }
    }
}
