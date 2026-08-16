using System;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;

namespace LostFound.AI.Ranking
{
    // Phase 2B Part 3's calibrator - see IConfidenceCalibrator's remarks on
    // how this relates to the existing LostFound.AI.ConfidenceCalibrator.
    // "Confidence must not equal raw similarity" (spec) is a structural
    // property of this formula, not a convention: sigmoid squashing means
    // the output is never a linear copy of the input, and the coverage
    // factor means two candidates with the identical weighted score but a
    // different NUMBER of contributing signals get different confidence -
    // a lone strong signal is treated as less trustworthy than several
    // moderate signals agreeing.
    internal sealed class ConfidenceCalibrator(IOptions<RankingOptions> options) : IConfidenceCalibrator
    {
        public double Calibrate(double rawRankingScore, RankingFeatures features, int contributingSignalCount)
        {
            var midpoint = options.Value.ConfidenceMidpoint;
            var temperature = Math.Max(options.Value.ConfidenceTemperature, 0.001);

            var sigmoid = 1.0 / (1.0 + Math.Exp(-(rawRankingScore - midpoint) / temperature));

            var expectedSignals = Math.Max(options.Value.ExpectedSignalCount, 1);
            var coverage = Math.Min(1.0, (double)contributingSignalCount / expectedSignals);

            return Math.Round(sigmoid * 100.0 * (0.5 + 0.5 * coverage), 1);
        }
    }
}
