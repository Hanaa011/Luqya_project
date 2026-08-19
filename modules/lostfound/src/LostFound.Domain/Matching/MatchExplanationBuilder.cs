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
                level = "very high semantic similarity / ����� ����� ���� ����";
            }
            else if (scorePercentage >= 80)
            {
                level = "high semantic similarity / ����� ����� ����";
            }
            else if (scorePercentage >= 70)
            {
                level = "moderate semantic similarity / ����� ����� �����";
            }
            else
            {
                level = "low semantic similarity / ����� ����� �����";
            }

            return $"AI match ({providerName}): {scorePercentage:0.00}% - {level}. Descriptions compared by meaning, not exact wording.";
        }

        // Phase 4 Part 3: honestly distinct from Build() above - this Match
        // was not produced by MatchManager's own background scoring pass at
        // all; a user found it through Smart Search and claimed it
        // themselves. Reusing Build()'s "AI match" wording here would
        // misrepresent the provenance, so this is a deliberately separate
        // template rather than a parameter added to the existing one.
        public static string BuildForUserClaim(double observedScorePercentage)
        {
            return $"Claimed via Smart Search by the reporting user, at {observedScorePercentage:0.00}% shown at the time - not an automatic AI match. / " +
                   "تم ربط هذه المطابقة يدويًا عبر البحث الذكي من قبل المستخدم، وليست مطابقة تلقائية بالذكاء الاصطناعي.";
        }
    }
}
