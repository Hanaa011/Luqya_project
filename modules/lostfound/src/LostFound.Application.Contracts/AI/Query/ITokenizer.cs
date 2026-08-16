using System.Collections.Generic;

namespace LostFound.AI.Query
{
    // General-purpose word tokenizer for query text - NOT the same as
    // Phase 2A Part 2's subword tokenizer (LostFound.AI.Runtime.TokenizerLoader),
    // which produces model-specific integer token IDs for ONNX inference.
    // This produces whole words for entity recognition/spell correction/
    // intent detection to operate on.
    public interface ITokenizer
    {
        IReadOnlyList<string> Tokenize(string text);
    }
}
