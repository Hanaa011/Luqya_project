using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Retrieval
{
    // The common contract every retrieval strategy implements - "every
    // strategy must be independently testable" (spec) means each concrete
    // class is resolvable and callable on its own, with no dependency on
    // the orchestration layer (ICandidateGenerator etc.) to exercise it.
    public interface IRetrievalStrategy
    {
        string StrategyName { get; }

        Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default);
    }

    // The five strategies the spec names as core/dedicated interfaces.
    // Declared as empty marker interfaces extending IRetrievalStrategy
    // (same reasoning as Phase 2A Part 5's storage marker interfaces) so
    // each is independently resolvable by its specific name via DI, without
    // a second parallel member list to keep in sync - the other eight named
    // strategies (Category/Brand/Color/Material/Location/Time/Alias/
    // Synonym) implement IRetrievalStrategy directly, since the spec itself
    // only singles out these five for a dedicated interface.
    public interface IVectorRetriever : IRetrievalStrategy
    {
    }

    public interface IBM25Retriever : IRetrievalStrategy
    {
    }

    public interface IGraphRetriever : IRetrievalStrategy
    {
    }

    public interface IFuzzyRetriever : IRetrievalStrategy
    {
    }

    public interface IExactRetriever : IRetrievalStrategy
    {
    }
}
