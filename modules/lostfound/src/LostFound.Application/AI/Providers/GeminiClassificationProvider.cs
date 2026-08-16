using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Gemini-backed <see cref="IItemClassificationProvider"/>. Prompt content
    /// lives in the shared <see cref="ClassificationPromptBuilder"/> so every
    /// provider asks for (and gets) the same JSON shape and the same
    /// <c>searchText</c> quality bar.
    /// </summary>
    public class GeminiClassificationProvider : IItemClassificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;

        public string ProviderName => "Gemini";

        public GeminiClassificationProvider(
            HttpClient httpClient,
            IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.Gemini
                ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<ItemClassificationResult> ClassifyAsync(
            string? description,
            byte[]? imageBytes,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("Gemini API Key is missing.");
            }

            if (string.IsNullOrWhiteSpace(_options.TextModel))
            {
                throw new InvalidOperationException("Gemini TextModel is missing.");
            }

            var prompt = ClassificationPromptBuilder.Build(description);

            var raw = await GeminiVisionHelper.GenerateTextAsync(
                _httpClient,
                _options.ApiKey,
                _options.TextModel,
                prompt,
                imageBytes,
                cancellationToken);

            return ClassificationJsonParser.Parse(raw);
        }
    }
}
