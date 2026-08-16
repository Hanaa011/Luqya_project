using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Decides which registered strategies actually run for a given query:
    // respects configuration (EnabledStrategies) and skips a strategy that
    // provably cannot contribute given this query's content (e.g. the Graph
    // retriever when the query pipeline resolved no concept to expand),
    // rather than running it only to always return empty.
    internal sealed class RetrievalPlanner(IOptions<RetrievalOptions> options) : IRetrievalPlanner
    {
        public RetrievalPlan Plan(SemanticQuery query, IReadOnlyList<IRetrievalStrategy> availableStrategies)
        {
            var enabled = options.Value.EnabledStrategies;
            var names = new List<string>();

            foreach (var strategy in availableStrategies)
            {
                if (!enabled.GetValueOrDefault(strategy.StrategyName, true))
                {
                    continue;
                }

                if (!CanContribute(strategy.StrategyName, query))
                {
                    continue;
                }

                names.Add(strategy.StrategyName);
            }

            return new RetrievalPlan(names, options.Value.PerStrategyLimit);
        }

        private static bool CanContribute(string strategyName, SemanticQuery query) => strategyName switch
        {
            "Graph" => query.ExpandedTerms.Count > 0,
            "Category" => query.Entities.Any(e => e.Type == EntityType.Category),
            "Brand" => query.Entities.Any(e => e.Type == EntityType.Brand),
            "Color" => query.Entities.Any(e => e.Type == EntityType.Color),
            "Material" => query.Entities.Any(e => e.Type == EntityType.Material),
            "Location" => query.Entities.Any(e => e.Type == EntityType.Location),
            "Time" => query.Entities.Any(e => e.Type == EntityType.DateTime),
            _ => true // BM25/Vector/Fuzzy/Exact always attempt to contribute
        };
    }
}
