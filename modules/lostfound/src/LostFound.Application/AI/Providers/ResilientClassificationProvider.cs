using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Decorates any <see cref="IItemClassificationProvider"/> with the
    /// retry/backoff behavior from <see cref="ResilientProviderDecorator"/>.
    /// See <see cref="ResilientEmbeddingProvider"/> for why Gemini is
    /// excluded from this wrapping.
    /// </summary>
    internal sealed class ResilientClassificationProvider : IItemClassificationProvider
    {
        private readonly IItemClassificationProvider _inner;
        private readonly ILogger<ResilientClassificationProvider> _logger;

        public ResilientClassificationProvider(IItemClassificationProvider inner, ILogger<ResilientClassificationProvider> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public string ProviderName => _inner.ProviderName;

        public Task<ItemClassificationResult> ClassifyAsync(
            string? description, byte[]? imageBytes, CancellationToken cancellationToken = default) =>
            ResilientProviderDecorator.ExecuteAsync(
                ct => _inner.ClassifyAsync(description, imageBytes, ct),
                $"{_inner.ProviderName} ClassifyAsync",
                _logger,
                cancellationToken);
    }
}
