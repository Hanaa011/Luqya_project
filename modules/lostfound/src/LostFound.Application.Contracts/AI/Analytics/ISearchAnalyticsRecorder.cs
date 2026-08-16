using System;
using System.Collections.Generic;

namespace LostFound.AI.Analytics
{
    // One record per completed FindSimilarReportsAsync call, from either
    // pipeline. Pipeline distinguishes "Legacy" (deterministic scoring,
    // still the default) from "Hybrid" (Phase 2B's new pipeline, behind
    // LostFound:AI:HybridPipeline:Enabled) so volume/latency/zero-result
    // rate can be compared side by side once the flag is flipped on for a
    // subset of traffic.
    public sealed record SearchEvent(
        DateTime TimestampUtc,
        string Pipeline,
        string? LanguageCode,
        long ElapsedMilliseconds,
        int ResultCount,
        bool ZeroResults);

    public sealed record SearchAnalyticsSnapshot(
        long TotalSearches,
        long HybridSearches,
        long LegacySearches,
        double AverageLatencyMilliseconds,
        double P95LatencyMilliseconds,
        double ZeroResultRate,
        IReadOnlyDictionary<string, long> LanguageDistribution);

    // Spec's "Monitoring & Analytics" deliverable, split from
    // IAiPlatformDiagnostics (Phase 2A Part 5, which reports subsystem
    // health/availability) - this tracks SEARCH TRAFFIC itself: volume,
    // latency, zero-result rate, language distribution. Real, in-memory,
    // thread-safe aggregation of events actually recorded by
    // AiMatchingService/SemanticSearchOrchestrator - not a fabricated
    // metrics surface.
    public interface ISearchAnalyticsRecorder
    {
        void Record(SearchEvent searchEvent);

        SearchAnalyticsSnapshot GetSnapshot();
    }
}
