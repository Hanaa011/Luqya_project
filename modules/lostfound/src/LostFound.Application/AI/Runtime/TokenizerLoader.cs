using System;
using System.IO;
using Microsoft.ML.Tokenizers;

namespace LostFound.AI.Runtime
{
    // Loads a Tokenizer from whichever format is present in the model
    // directory. Deliberately format-sniffing rather than hardcoded to one
    // model family, since Phase 1 Part 6 names BGE-M3 as primary and
    // multilingual-e5-base as a documented fallback, and different exports
    // ship different tokenizer formats.
    internal static class TokenizerLoader
    {
        public static Tokenizer Load(string tokenizerFilePath)
        {
            var extension = Path.GetExtension(tokenizerFilePath);

            return extension.ToLowerInvariant() switch
            {
                ".model" => LoadSentencePiece(tokenizerFilePath),
                ".txt" => BertTokenizer.Create(tokenizerFilePath, new BertOptions()),
                _ => throw new NotSupportedException(
                    $"Unsupported tokenizer file format '{extension}' at '{tokenizerFilePath}'. " +
                    "Expected a SentencePiece '.model' file or a WordPiece 'vocab.txt' file.")
            };
        }

        private static Tokenizer LoadSentencePiece(string path)
        {
            using var stream = File.OpenRead(path);

            // SentencePieceTokenizer.Create loads the standard SentencePiece
            // protobuf ".model" format regardless of the trainer's
            // model_type. This matters because BGE-M3/multilingual-e5's
            // XLM-RoBERTa tokenizer ("sentencepiece.bpe.model", despite the
            // filename) is trained with SentencePiece's Unigram algorithm,
            // not Bpe - LlamaTokenizer.Create (used here previously) hard-
            // rejects anything but model_type == Bpe and throws
            // ArgumentException("The model type is not Bpe.") for this
            // model. Verified against the manually installed BGE-M3 export:
            // ModelProto.TrainerSpec.ModelType == Unigram. Begin/end-of-
            // sentence tokens are handled by the model-specific input
            // format, not added implicitly here.
            return SentencePieceTokenizer.Create(stream, addBeginningOfSentence: false, addEndOfSentence: false, specialTokens: null);
        }
    }
}
