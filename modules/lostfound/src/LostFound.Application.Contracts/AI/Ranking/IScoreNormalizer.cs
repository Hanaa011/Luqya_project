namespace LostFound.AI.Ranking
{
    // Maps every raw feature onto a comparable 0-1 scale before weighting -
    // most features already come in as 0-100 percentages (a simple /100),
    // but BM25's raw score is unbounded, so it needs its own compression
    // (see ScoreNormalizer's remarks).
    public interface IScoreNormalizer
    {
        RankingFeatures Normalize(RankingFeatures rawFeatures);
    }
}
