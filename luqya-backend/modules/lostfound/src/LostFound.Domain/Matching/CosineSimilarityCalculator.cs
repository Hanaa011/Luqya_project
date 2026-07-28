using System;

namespace LostFound.Matching
{
    public static class CosineSimilarityCalculator
    {
        public static double CalculatePercentage(float[]? a, float[]? b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            {
                return 0;
            }

            double dot = 0, magnitudeA = 0, magnitudeB = 0;

            for (var i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
            {
                return 0;
            }

            var cosine = dot / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
            cosine = Math.Max(-1, Math.Min(1, cosine));

            return Math.Round((cosine + 1) / 2 * 100, 2);
        }
    }
}
