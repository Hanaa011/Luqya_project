using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Decorates any <see cref="IEmbeddingProvider"/> with the retry/backoff
    /// behavior from <see cref="ResilientProviderDecorator"/>. Applied by
    /// <see cref="LostFoundAiProvidersServiceCollectionExtensions"/> only to
    /// providers that don't already implement their own resilience - see
    /// <see cref="AiProviderRegistry.SelfResilientProviders"/> (Gemini already
    /// retries internally via <see cref="GeminiVisionHelper"/>; wrapping it
    /// again here would compound retries and regress worst-case latency
    /// against <c>AiMatchingService.AiCallTimeout</c>).
    /// </summary>
    internal sealed class ResilientEmbeddingProvider : IEmbeddingProvider
    {
        private readonly IEmbeddingProvider _inner;
        private readonly ILogger<ResilientEmbeddingProvider> _logger;

        public ResilientEmbeddingProvider(IEmbeddingProvider inner, ILogger<ResilientEmbeddingProvider> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public string ProviderName => _inner.ProviderName;

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            ResilientProviderDecorator.ExecuteAsync(
                ct => _inner.GenerateEmbeddingAsync(text, ct),
                $"{_inner.ProviderName} GenerateEmbeddingAsync",
                _logger,
                cancellationToken);

        public Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default) =>
            ResilientProviderDecorator.ExecuteAsync(
                ct => _inner.GenerateImageEmbeddingAsync(imageBytes, ct),
                $"{_inner.ProviderName} GenerateImageEmbeddingAsync",
                _logger,
                cancellationToken);
    }
}
