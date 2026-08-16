using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// DeepSeek-backed <see cref="IItemClassificationProvider"/> (OpenAI-
    /// compatible <c>/chat/completions</c>). Uses the same shared
    /// <see cref="ClassificationPromptBuilder"/>/<see cref="ClassificationJsonParser"/>
    /// as every other provider. Text-only - DeepSeek's public API has no
    /// reliable image input at the time this was written, so
    /// <paramref name="imageBytes"/> being supplied without
    /// <paramref name="description"/> will yield a weak/empty classification.
    /// Prefer pairing DeepSeek classification with searches that include text.
    /// </summary>
    public class DeepSeekClassificationProvider : IItemClassificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly DeepSeekOptions _options;

        public string ProviderName => "DeepSeek";

        public DeepSeekClassificationProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.DeepSeek
                ?? throw new ArgumentNullException(nameof(options));
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        public async Task<ItemClassificationResult> ClassifyAsync(string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("DeepSeek API Key is missing.");
            }

            var prompt = ClassificationPromptBuilder.Build(description);

            var raw = await DeepSeekVisionHelper.GenerateAsync(_httpClient, _options.ChatModel, prompt, imageBytes, cancellationToken);
            return ClassificationJsonParser.Parse(raw);
        }
    }
}
