using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI
{
    // Local-first classification fallback contract - PHASE-VALIDATION-06.
    // Same shape as IItemClassificationProvider, but registered as its own
    // DI type so it never collides with the single configured remote
    // IItemClassificationProvider (AiProviderRegistry only ever registers
    // one of those, selected by "LostFound:AI:Provider"). ClassificationEngine
    // depends on both: the remote provider first, this one as the automatic,
    // provider-agnostic fallback whenever the remote provider fails for any
    // reason (timeout, HTTP failure, auth failure, quota, no subscription,
    // provider unavailable) - see LostFound.AI.Core.ClassificationEngine.
    //
    // Implemented by LostFound.AI.Providers.LocalClassificationProvider
    // (LostFound.Application), which classifies entirely offline using the
    // existing local query pipeline (entity recognition, ontology/concept
    // resolution, knowledge graph) and the existing local ONNX embedding
    // runtime for semantic similarity - no new inference engine, no new
    // external dependency.
    public interface ILocalClassificationProvider : IItemClassificationProvider
    {
    }
}
