using System;
using Shouldly;
using Xunit;

namespace LostFound.AI.Analytics;

// Phase 2B Part 4 - pure, dependency-free class (no DB, no DI needed), so
// this test constructs it directly rather than going through the ABP test
// base.
public class InMemorySearchAnalyticsRecorderTests
{
    [Fact]
    public void Aggregates_volume_pipeline_split_zero_result_rate_and_language_distribution()
    {
        var recorder = new InMemorySearchAnalyticsRecorder();

        recorder.Record(new SearchEvent(DateTime.UtcNow, "Hybrid", "en", 10, ResultCount: 3, ZeroResults: false));
        recorder.Record(new SearchEvent(DateTime.UtcNow, "Hybrid", "ar", 20, ResultCount: 0, ZeroResults: true));
        recorder.Record(new SearchEvent(DateTime.UtcNow, "Legacy", "en", 30, ResultCount: 5, ZeroResults: false));

        var snapshot = recorder.GetSnapshot();

        snapshot.TotalSearches.ShouldBe(3);
        snapshot.HybridSearches.ShouldBe(2);
        snapshot.LegacySearches.ShouldBe(1);
        snapshot.AverageLatencyMilliseconds.ShouldBe(20.0);
        snapshot.ZeroResultRate.ShouldBe(1.0 / 3.0, 0.0001);
        snapshot.LanguageDistribution["en"].ShouldBe(2);
        snapshot.LanguageDistribution["ar"].ShouldBe(1);
    }

    [Fact]
    public void Returns_a_zeroed_snapshot_when_nothing_has_been_recorded()
    {
        var recorder = new InMemorySearchAnalyticsRecorder();

        var snapshot = recorder.GetSnapshot();

        snapshot.TotalSearches.ShouldBe(0);
        snapshot.AverageLatencyMilliseconds.ShouldBe(0);
        snapshot.P95LatencyMilliseconds.ShouldBe(0);
        snapshot.ZeroResultRate.ShouldBe(0);
    }

    [Fact]
    public void Computes_a_real_p95_over_the_recent_latency_window()
    {
        var recorder = new InMemorySearchAnalyticsRecorder();

        for (var i = 1; i <= 100; i++)
        {
            recorder.Record(new SearchEvent(DateTime.UtcNow, "Hybrid", "en", i, ResultCount: 1, ZeroResults: false));
        }

        var snapshot = recorder.GetSnapshot();

        // Latencies 1..100ms - the 95th percentile should land at/near 95ms.
        snapshot.P95LatencyMilliseconds.ShouldBeInRange(94, 96);
    }
}
