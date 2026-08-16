using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Query
{
    // The single entry point for the whole pipeline described in the Phase
    // 2B Part 1 spec: Language Detection -> Unicode Normalization ->
    // Language-Specific Normalization -> Spell Correction -> Transliteration
    // -> Tokenization -> Lemmatization -> Intent Detection -> Entity
    // Recognition -> Concept Resolution -> Knowledge Graph Expansion ->
    // Synonym Expansion -> Semantic Query Builder. Does not call
    // IEmbeddingEngine itself (spec: "does NOT implement retrieval") -
    // SemanticQuery.FinalSemanticText is the prepared input for whichever
    // later stage does.
    public interface IQueryPipeline
    {
        Task<SemanticQuery> ProcessAsync(string rawText, CancellationToken cancellationToken = default);
    }
}
