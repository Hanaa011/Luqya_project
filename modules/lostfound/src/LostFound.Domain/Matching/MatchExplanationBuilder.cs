namespace LostFound.Matching
{
    // Templated, deterministic explanation text - NOT an AI call. Keeps the
    // Domain layer independent from any AI provider.
    public static class MatchExplanationBuilder
    {
        public static string Build(double scorePercentage, string providerName)
        {
            string level;

            if (scorePercentage >= 90)
            {
                level = "very high semantic similarity /  ÿ«»ﬁ œ·«·Ì ⁄«·Ú Ãœ«";
            }
            else if (scorePercentage >= 80)
            {
                level = "high semantic similarity /  ÿ«»ﬁ œ·«·Ì ⁄«·Ú";
            }
            else if (scorePercentage >= 70)
            {
                level = "moderate semantic similarity /  ÿ«»ﬁ œ·«·Ì „ Ê”ÿ";
            }
            else
            {
                level = "low semantic similarity /  ÿ«»ﬁ œ·«·Ì „‰Œ›÷";
            }

            return $"AI match ({providerName}): {scorePercentage:0.00}% - {level}. Descriptions compared by meaning, not exact wording.";
        }
    }
}
