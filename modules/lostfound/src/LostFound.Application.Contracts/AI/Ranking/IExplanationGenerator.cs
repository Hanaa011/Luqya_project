using System.Collections.Generic;

namespace LostFound.AI.Ranking
{
    // "No opaque scoring" (spec) - every RankedResult must be able to
    // explain itself: which signals fired, how strong each was, and
    // whether semantic (embedding) or graph evidence contributed, not just
    // a final number.
    public interface IExplanationGenerator
    {
        // languageCode is the query's detected language (ILanguageDetector -
        // "ar"/"en"/"ur" today; see HeuristicLanguageDetector), so the
        // explanation text follows the query the user actually typed rather
        // than a fixed UI culture. Unrecognized codes fall back to English.
        RankingExplanation Generate(
            RankingFeatures normalizedFeatures,
            IReadOnlyDictionary<string, double> weights,
            double confidence,
            string languageCode);
    }
}
