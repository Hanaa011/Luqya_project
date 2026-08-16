using System.Collections.Generic;

namespace LostFound.AI.Configuration
{
    // Bound from configuration section "LostFound:AI:LocalRuntime". Every
    // path is relative to the host's content root unless rooted. Tensor
    // input/output names default to the convention most HuggingFace ONNX
    // exports of BERT/XLM-RoBERTa-family models use - verify against the
    // actual exported model's I/O names (e.g. via Netron) before enabling in
    // production; a name mismatch here fails loudly at load time
    // (see OnnxEmbeddingModel.Load) rather than silently producing wrong
    // embeddings.
    public class LocalAiRuntimeOptions
    {
        // Master switch - false makes IEmbeddingRuntime report NotInstalled
        // unconditionally, so LocalFirstEmbeddingEngine always uses the
        // external provider without ever touching the filesystem/ONNX.
        public bool Enabled { get; set; } = false;

        public string ModelDirectory { get; set; } = "AI-Models/Embeddings";

        public string DatabasePath { get; set; } = "AI-Data/embeddings.db";

        public string ModelFileName { get; set; } = "model.onnx";

        // Expected format: a raw SentencePiece ".model" protobuf (what
        // BGE-M3/multilingual-e5's XLM-RoBERTa tokenizer ships as) - see
        // TokenizerLoader. A WordPiece "vocab.txt" is also supported for a
        // BERT-family alternative.
        public string TokenizerFileName { get; set; } = "sentencepiece.bpe.model";

        // BGE-M3's dense output dimension - see Phase 1 Part 6's model
        // choice. Update if a different model is installed.
        public int EmbeddingDimensions { get; set; } = 1024;

        public List<string> SupportedLanguages { get; set; } = new() { "ar", "en", "ur" };

        public string InputIdsTensorName { get; set; } = "input_ids";

        public string AttentionMaskTensorName { get; set; } = "attention_mask";

        // Only sent to the model if the loaded ONNX graph actually declares
        // this input (BERT-style models need it, XLM-RoBERTa-style models
        // like BGE-M3 typically don't) - see OnnxEmbeddingModel.Load.
        public string TokenTypeIdsTensorName { get; set; } = "token_type_ids";

        // BGE-M3's official ONNX export (huggingface.co/BAAI/bge-m3/tree/main/onnx)
        // names its outputs "token_embeddings" (per-token, [batch, seq, hidden])
        // and "sentence_embedding" (already mean-pooled, [batch, hidden]) - not
        // the generic HuggingFace "last_hidden_state" this defaulted to before.
        // "token_embeddings" is used here since OnnxEmbeddingModel does its own
        // mean-pool + L2-normalize over the per-token tensor.
        public string OutputTensorName { get; set; } = "token_embeddings";

        public int MaxSequenceLength { get; set; } = 512;

        public int MemoryCacheMaxEntries { get; set; } = 2000;
    }
}
