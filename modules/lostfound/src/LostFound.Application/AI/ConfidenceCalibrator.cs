using System;

namespace LostFound.AI
{
    /// <summary>
    /// Remaps a raw hybrid score (0-100, occasionally slightly outside that
    /// range once dynamic boosts are added) onto a calibrated confidence
    /// percentage that actually reads the way a person expects: an obvious,
    /// high-confidence match should land in the 90s, not the low 70s.
    ///
    /// Deliberately NOT a single multiply-by-constant - that would just move
    /// every band by the same amount and still compress obvious matches. This
    /// is a monotonic piecewise-linear curve through a handful of control
    /// points, which stretches the upper-middle of the raw range (where most
    /// genuinely good matches used to cluster around ~70) out into the 90-98
    /// band, while still leaving weak/bad matches near the bottom.
    /// Monotonic by construction, so it can never change candidate ranking -
    /// callers should still rank by the raw score and calibrate only for
    /// display.
    /// </summary>
    internal static class ConfidenceCalibrator
    {
        /// <summary>
        /// (rawScore, calibratedScore) control points, ascending by raw
        /// score. Chosen so that the bands from the spec line up:
        /// excellent 90-98, good 80-90, average 60-80, weak 40-60, bad &lt;40.
        /// </summary>
        private static readonly (double Raw, double Calibrated)[] ControlPoints =
        {
            (0, 0),
            (20, 40),
            (35, 60),
            (50, 80),
            (65, 90),
            (100, 98)
        };

        public static double Calibrate(double rawScore)
        {
            var clamped = Math.Clamp(rawScore, 0, 100);

            for (var i = 1; i < ControlPoints.Length; i++)
            {
                var (prevRaw, prevCalibrated) = ControlPoints[i - 1];
                var (nextRaw, nextCalibrated) = ControlPoints[i];

                if (clamped > nextRaw && i != ControlPoints.Length - 1)
                {
                    continue;
                }

                var span = nextRaw - prevRaw;
                var t = span <= 0 ? 0 : (clamped - prevRaw) / span;

                return prevCalibrated + (t * (nextCalibrated - prevCalibrated));
            }

            // Unreachable - the loop above always returns once it reaches the
            // final control point - present only to satisfy flow analysis.
            return clamped;
        }
    }
}
