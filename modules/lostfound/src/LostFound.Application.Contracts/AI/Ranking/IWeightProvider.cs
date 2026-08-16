using System.Collections.Generic;

namespace LostFound.AI.Ranking
{
    // Runtime-configurable per-feature weights ("Adaptive Weights" / "no
    // recompilation" / "prepare for A/B testing" - spec). The optional
    // variant parameter is the A/B-testing seam: a caller can request a
    // named weight set (e.g. an experiment arm) without this interface
    // needing to change once real experimentation infrastructure exists -
    // today only the default (null) variant is actually configured.
    public interface IWeightProvider
    {
        IReadOnlyDictionary<string, double> GetWeights(string? variant = null);
    }
}
