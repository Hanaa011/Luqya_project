using System.Collections.Generic;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Decides which registered strategies actually run for a given query
    // and how many candidates each may return - e.g. skipping the graph
    // retriever entirely when the query pipeline resolved no concept
    // (ISemanticExpander would have nothing to expand from), rather than
    // running it to always return zero results.
    public interface IRetrievalPlanner
    {
        RetrievalPlan Plan(SemanticQuery query, IReadOnlyList<IRetrievalStrategy> availableStrategies);
    }
}
