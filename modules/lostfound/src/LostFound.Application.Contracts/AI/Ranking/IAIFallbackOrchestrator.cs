namespace LostFound.AI.Ranking
{
    // Classifies which tier of the Phase 1 Part 7 degradation ladder
    // characterizes a completed search's result quality, from the
    // diagnostics IRankingEngine already collected - see FallbackTier's own
    // remarks for why this doesn't re-implement fallback control flow.
    public interface IAIFallbackOrchestrator
    {
        FallbackTier DetermineTier(RankingDiagnostics diagnostics, string embeddingEngineName, bool crossEncoderUsed);
    }
}
