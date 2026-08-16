using System.Threading;
using System.Threading.Tasks;
using LostFound.AI;

namespace LostFound.AI.Core
{
    // Capability abstraction introduced in Phase 2A Part 1 (Enterprise AI
    // Foundation): callers depend on "classify this item", never on WHICH
    // provider does it. Today this is implemented as an ordered fallback
    // chain over one or more IItemClassificationProvider instances
    // (configured primary, then Gemini as a dedicated second remote tier),
    // falling through to a local provider only if every remote tier fails -
    // see LostFound.AI.Core.ClassificationEngine in LostFound.Application
    // for the full chain and its exception-only fallback semantics.
    public interface IClassificationEngine
    {
        Task<ItemClassificationResult> ClassifyAsync(
            string? description,
            byte[]? imageBytes,
            CancellationToken cancellationToken = default);
    }
}
