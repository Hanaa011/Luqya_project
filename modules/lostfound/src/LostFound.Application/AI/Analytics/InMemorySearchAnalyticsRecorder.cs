using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using LostFound.AI.Analytics;

namespace LostFound.AI.Analytics
{
    // Singleton, thread-safe, real (not simulated) aggregation. Cumulative
    // counters use Interlocked so Record() never blocks a search request on
    // a lock; the bounded recent-latency buffer (needed for a real P95, not
    // just an average) is the only part that takes a short lock, sized so
    // that lock is held for microseconds even under concurrent search load.
    internal sealed class InMemorySearchAnalyticsRecorder : ISearchAnalyticsRecorder
    {
        private const int RecentLatencyCapacity = 2000;

        private readonly object _latencyLock = new();
        private readonly long[] _recentLatencies = new long[RecentLatencyCapacity];
        private int _recentLatencyCount;
        private int _recentLatencyCursor;

        private long _totalSearches;
        private long _hybridSearches;
        private long _legacySearches;
        private long _zeroResultSearches;
        private long _latencySumMs;

        private readonly ConcurrentDictionary<string, long> _languageDistribution = new(StringComparer.OrdinalIgnoreCase);

        public void Record(SearchEvent searchEvent)
        {
            Interlocked.Increment(ref _totalSearches);
            Interlocked.Add(ref _latencySumMs, searchEvent.ElapsedMilliseconds);

            if (string.Equals(searchEvent.Pipeline, "Hybrid", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _hybridSearches);
            }
            else
            {
                Interlocked.Increment(ref _legacySearches);
            }

            if (searchEvent.ZeroResults)
            {
                Interlocked.Increment(ref _zeroResultSearches);
            }

            var language = string.IsNullOrWhiteSpace(searchEvent.LanguageCode) ? "unknown" : searchEvent.LanguageCode!;
            _languageDistribution.AddOrUpdate(language, 1, (_, count) => count + 1);

            lock (_latencyLock)
            {
                _recentLatencies[_recentLatencyCursor] = searchEvent.ElapsedMilliseconds;
                _recentLatencyCursor = (_recentLatencyCursor + 1) % RecentLatencyCapacity;
                _recentLatencyCount = Math.Min(_recentLatencyCount + 1, RecentLatencyCapacity);
            }
        }

        public SearchAnalyticsSnapshot GetSnapshot()
        {
            var total = Interlocked.Read(ref _totalSearches);
            var latencySum = Interlocked.Read(ref _latencySumMs);
            var averageLatency = total == 0 ? 0 : (double)latencySum / total;
            var zeroResultRate = total == 0 ? 0 : (double)Interlocked.Read(ref _zeroResultSearches) / total;

            return new SearchAnalyticsSnapshot(
                total,
                Interlocked.Read(ref _hybridSearches),
                Interlocked.Read(ref _legacySearches),
                averageLatency,
                ComputeP95Latency(),
                zeroResultRate,
                new Dictionary<string, long>(_languageDistribution));
        }

        private double ComputeP95Latency()
        {
            long[] snapshot;
            int count;

            lock (_latencyLock)
            {
                if (_recentLatencyCount == 0)
                {
                    return 0;
                }

                count = _recentLatencyCount;
                snapshot = new long[count];
                Array.Copy(_recentLatencies, snapshot, count);
            }

            Array.Sort(snapshot);
            var index = (int)Math.Ceiling(0.95 * count) - 1;
            index = Math.Clamp(index, 0, count - 1);
            return snapshot[index];
        }
    }
}
