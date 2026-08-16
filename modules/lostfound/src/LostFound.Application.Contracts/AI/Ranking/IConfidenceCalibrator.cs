namespace LostFound.AI.Ranking
{
    // Phase 2B Part 3's confidence calibrator - distinct from the existing
    // LostFound.AI.ConfidenceCalibrator (a static class AiMatchingService
    // still uses today; Phase 2B Part 4 is the integration Part that
    // decides whether/how to retire it). "Confidence must not equal raw
    // similarity" (spec) - see ConfidenceCalibrator's implementation for
    // the actual transform (sigmoid squashing + signal-agreement coverage
    // factor), which structurally guarantees this by construction.
    public interface IConfidenceCalibrator
    {
        double Calibrate(double rawRankingScore, RankingFeatures features, int contributingSignalCount);
    }
}
