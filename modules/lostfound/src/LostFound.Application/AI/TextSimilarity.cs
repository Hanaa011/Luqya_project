using System;

namespace LostFound.AI
{
    // Shared Levenshtein edit-distance implementation - used by Phase 2B
    // Part 1's DictionarySpellCorrectionService (correcting a query token
    // against the known concept vocabulary) and Phase 2B Part 2's
    // FuzzyRetriever (scoring how close a query token is to report text),
    // extracted here rather than duplicated in both.
    internal static class TextSimilarity
    {
        public static int LevenshteinDistance(string a, string b)
        {
            var lengthA = a.Length;
            var lengthB = b.Length;
            var distances = new int[lengthA + 1, lengthB + 1];

            for (var i = 0; i <= lengthA; i++)
            {
                distances[i, 0] = i;
            }

            for (var j = 0; j <= lengthB; j++)
            {
                distances[0, j] = j;
            }

            for (var i = 1; i <= lengthA; i++)
            {
                for (var j = 1; j <= lengthB; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    distances[i, j] = Math.Min(
                        Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                        distances[i - 1, j - 1] + cost);
                }
            }

            return distances[lengthA, lengthB];
        }
    }
}
