using System;
using System.Collections.Generic;
using LostFound.AI.Ontology;

namespace LostFound.AI.Ranking
{
    // The 14 features the spec names, each on a 0-100 scale (matching the
    // existing CosineSimilarityCalculator/AiMatchingService convention).
    // HistoricalSuccess and Popularity are documented, honest placeholders:
    // no report-level "was this a successful match" or popularity signal
    // exists anywhere in the current domain model (Match/MatchStatus tracks
    // match outcomes, not aggregated per-report success history) - both
    // return a neutral 0 rather than a fabricated value. Real backing data
    // for either is a concrete, well-scoped future addition, not something
    // to guess at here.
    public sealed record RankingFeatures(
        double EmbeddingSimilarity,
        double MetadataEmbeddingSimilarity,
        double Bm25Score,
        double KnowledgeGraphSimilarity,
        double ObjectTypeSimilarity,
        double CategorySimilarity,
        double BrandSimilarity,
        double ColorSimilarity,
        double MaterialSimilarity,
        double LocationSimilarity,
        double TimeProximity,
        double AliasMatch,
        double ExactMatch,
        double HistoricalSuccess,
        double Popularity);

    public enum CrossEncoderStatus
    {
        NotAvailable,
        Available
    }

    // Matches Phase 1 Part 7's documented 8-tier graceful-degradation
    // ladder verbatim. Not a re-implementation of fallback control flow
    // (that already lives distributed across Phase 2A Part 1/2's
    // IEmbeddingEngine and Phase 2B Part 2's per-strategy failure
    // isolation) - a classification of what actually happened during THIS
    // search, for diagnostics/observability ("search must never fail" needs
    // to be verifiable, not just asserted).
    public enum FallbackTier
    {
        ExternalAi,
        CachedAi,
        LocalCrossEncoder,
        LocalEmbeddings,
        KnowledgeGraph,
        HybridRanking,
        Bm25Only,
        KeywordMatchOnly
    }

    public sealed record FeatureContribution(string FeatureName, double NormalizedValue, double Weight, double WeightedContribution);

    public sealed record RankingExplanation(
        string Summary,
        IReadOnlyList<string> StrongestSignals,
        IReadOnlyList<FeatureContribution> FeatureContributions,
        bool HasSemanticEvidence,
        bool HasGraphEvidence);

    // Compatibility is computed once, during ranking (PHASE-VALIDATION-06),
    // and carried on the result so both the actual ranking ORDER and the
    // displayed confidence/reasons (SemanticSearchOrchestrator) reflect the
    // exact same object-type-mismatch judgment - see RankingEngine.RankAsync.
    public sealed record RankedResult(
        Guid ReportId,
        double Confidence,
        RankingFeatures Features,
        RankingExplanation Explanation,
        ObjectTypeCompatibility Compatibility);

    public sealed record RankingDiagnostics(
        IReadOnlyDictionary<Guid, RankingFeatures> FeatureValuesByReport,
        long FeatureExtractionMs,
        long RerankingMs,
        long TotalRankingMs,
        IReadOnlyList<double> ConfidenceDistribution,
        FallbackTier FallbackTier,
        long ExplanationGenerationMs);

    public sealed record RankingResult(IReadOnlyList<RankedResult> Results, RankingDiagnostics Diagnostics);
}
