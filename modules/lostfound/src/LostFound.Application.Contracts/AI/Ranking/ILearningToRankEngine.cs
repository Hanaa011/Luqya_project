using System.Collections.Generic;

namespace LostFound.AI.Ranking
{
    // "Design an extensible LTR layer... Do not hardcode ranking logic"
    // (spec). The weighted-linear combination is the real, working default
    // implementation (LinearLearningToRankEngine) - not a stub - but the
    // scoring FORMULA lives entirely behind this one interface, so a future
    // trained ranking model is a new implementation registered in its
    // place, with zero change to IRankingEngine or anything upstream.
    public interface ILearningToRankEngine
    {
        double Score(RankingFeatures normalizedFeatures, IReadOnlyDictionary<string, double> weights);
    }
}
