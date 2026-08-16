using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using LostFound.AI.Configuration;
using LostFound.AI.Models;

namespace LostFound.AI.Runtime
{
    // A loaded ONNX embedding model: an InferenceSession plus its matching
    // Tokenizer. Encoding follows the standard sentence-embedding recipe
    // (mean-pool the last hidden state over the attention mask, then
    // L2-normalize) that BGE-M3/multilingual-e5 - the models Phase 1 Part 6
    // selected - both use.
    internal sealed class OnnxEmbeddingModel : IEmbeddingModel
    {
        private readonly InferenceSession _session;
        private readonly Tokenizer _tokenizer;
        private readonly string _inputIdsName;
        private readonly string _attentionMaskName;
        private readonly string? _tokenTypeIdsName;
        private readonly string _outputName;
        private readonly int _maxSequenceLength;
        private bool _disposed;

        public string Name { get; }
        public string Version { get; }
        public int Dimensions { get; }
        public IReadOnlyList<string> SupportedLanguages { get; }

        private OnnxEmbeddingModel(
            InferenceSession session,
            Tokenizer tokenizer,
            string name,
            string version,
            int dimensions,
            IReadOnlyList<string> supportedLanguages,
            string inputIdsName,
            string attentionMaskName,
            string? tokenTypeIdsName,
            string outputName,
            int maxSequenceLength)
        {
            _session = session;
            _tokenizer = tokenizer;
            Name = name;
            Version = version;
            Dimensions = dimensions;
            SupportedLanguages = supportedLanguages;
            _inputIdsName = inputIdsName;
            _attentionMaskName = attentionMaskName;
            _tokenTypeIdsName = tokenTypeIdsName;
            _outputName = outputName;
            _maxSequenceLength = maxSequenceLength;
        }

        /// <summary>
        /// Loads the ONNX model + tokenizer referenced by
        /// <paramref name="info"/>.<c>InstallPath</c>. Throws if either file
        /// is missing - callers (<see cref="OnnxEmbeddingRuntime"/>) must
        /// treat that as "not available", not a fatal startup error.
        /// </summary>
        public static OnnxEmbeddingModel Load(EmbeddingModelInfo info, LocalAiRuntimeOptions options)
        {
            if (info.InstallPath == null)
            {
                throw new InvalidOperationException(
                    $"Embedding model '{info.Name}' v{info.Version} has no install path recorded.");
            }

            var modelPath = Path.Combine(info.InstallPath, options.ModelFileName);
            var tokenizerPath = Path.Combine(info.InstallPath, options.TokenizerFileName);

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException(
                    $"Embedding model file not found for '{info.Name}' v{info.Version}.", modelPath);
            }

            if (!File.Exists(tokenizerPath))
            {
                throw new FileNotFoundException(
                    $"Tokenizer file not found for '{info.Name}' v{info.Version}.", tokenizerPath);
            }

            var sessionOptions = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };

            // GPU extension point (spec: "GPU support must be extensible"):
            // once a CUDA-capable deployment target exists, append the
            // execution provider here, e.g.
            // sessionOptions.AppendExecutionProvider_CUDA(0);
            // CPU-only for now, matching the "CPU inference is required"
            // mandate.
            var session = new InferenceSession(modelPath, sessionOptions);
            var tokenizer = TokenizerLoader.Load(tokenizerPath);

            var tokenTypeIdsName = session.InputMetadata.ContainsKey(options.TokenTypeIdsTensorName)
                ? options.TokenTypeIdsTensorName
                : null;

            return new OnnxEmbeddingModel(
                session,
                tokenizer,
                info.Name,
                info.Version,
                options.EmbeddingDimensions,
                options.SupportedLanguages,
                options.InputIdsTensorName,
                options.AttentionMaskTensorName,
                tokenTypeIdsName,
                options.OutputTensorName,
                options.MaxSequenceLength);
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = _tokenizer.EncodeToIds(text, considerPreTokenization: true, considerNormalization: true);
            var tokenCount = Math.Min(ids.Count, _maxSequenceLength);

            var inputIds = new DenseTensor<long>(new[] { 1, tokenCount });
            var attentionMask = new DenseTensor<long>(new[] { 1, tokenCount });

            for (var i = 0; i < tokenCount; i++)
            {
                inputIds[0, i] = ids[i];
                attentionMask[0, i] = 1;
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputIdsName, inputIds),
                NamedOnnxValue.CreateFromTensor(_attentionMaskName, attentionMask)
            };

            if (_tokenTypeIdsName != null)
            {
                // All-zero token-type-ids is correct for a single-segment
                // (non-sentence-pair) input, which is every embedding call
                // this model makes.
                inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeIdsName, new DenseTensor<long>(new[] { 1, tokenCount })));
            }

            using var outputs = _session.Run(inputs);

            var output = outputs.FirstOrDefault(o => o.Name == _outputName)
                ?? throw new InvalidOperationException(
                    $"ONNX model '{Name}' did not produce an output named '{_outputName}'. " +
                    $"Available outputs: {string.Join(", ", outputs.Select(o => o.Name))}.");

            var hiddenState = output.AsTensor<float>();

            // PHASE-VALIDATION-05 Goal C.3/C.4: the BGE-M3 ONNX export exposes
            // an already-pooled "sentence_embedding" output ([batch, hidden])
            // alongside the per-token "token_embeddings" output ([batch, seq,
            // hidden]) - see LocalAiRuntimeOptions.OutputTensorName. Rank
            // distinguishes which one is configured: rank 2 is already pooled
            // (just copy + normalize), rank 3 needs this class's own
            // mean-pool. This keeps both output names supported without a
            // separate config flag.
            var pooled = hiddenState.Dimensions.Length == 2
                ? ExtractPooled(hiddenState)
                : MeanPool(hiddenState, attentionMask, tokenCount);
            Normalize(pooled);

            return Task.FromResult(pooled);
        }

        private static float[] ExtractPooled(Tensor<float> hiddenState)
        {
            var hiddenSize = hiddenState.Dimensions[1];
            var pooled = new float[hiddenSize];

            for (var d = 0; d < hiddenSize; d++)
            {
                pooled[d] = hiddenState[0, d];
            }

            return pooled;
        }

        private static float[] MeanPool(Tensor<float> hiddenState, DenseTensor<long> attentionMask, int tokenCount)
        {
            var hiddenSize = hiddenState.Dimensions[2];
            var pooled = new float[hiddenSize];
            float maskSum = 0;

            for (var t = 0; t < tokenCount; t++)
            {
                var mask = attentionMask[0, t];
                if (mask == 0)
                {
                    continue;
                }

                maskSum += mask;
                for (var d = 0; d < hiddenSize; d++)
                {
                    pooled[d] += hiddenState[0, t, d] * mask;
                }
            }

            if (maskSum > 0)
            {
                for (var d = 0; d < hiddenSize; d++)
                {
                    pooled[d] /= maskSum;
                }
            }

            return pooled;
        }

        private static void Normalize(float[] vector)
        {
            double sumSquares = 0;
            foreach (var value in vector)
            {
                sumSquares += value * value;
            }

            var norm = Math.Sqrt(sumSquares);
            if (norm <= 0)
            {
                return;
            }

            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] = (float)(vector[i] / norm);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _session.Dispose();
            _disposed = true;
        }
    }
}
