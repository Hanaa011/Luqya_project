using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    public class GeminiEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;

        public string ProviderName => "Gemini";

        public GeminiEmbeddingProvider(
            HttpClient httpClient,
            IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.Gemini
                ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<float>();
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("Gemini API Key is missing.");
            }

            if (string.IsNullOrWhiteSpace(_options.EmbeddingModel))
            {
                throw new InvalidOperationException("Gemini EmbeddingModel is missing.");
            }

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{_options.EmbeddingModel}:embedContent?key={_options.ApiKey}";

            var requestBody = new
            {
                model = $"models/{_options.EmbeddingModel}",
                content = new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = text
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                url,
                requestBody,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Gemini Embedding Error ({(int)response.StatusCode})\n{body}");
            }

            var result =
                await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(
                    cancellationToken: cancellationToken);

            return result?.Embedding?.Values?
                       .Select(v => (float)v)
                       .ToArray()
                   ?? Array.Empty<float>();
        }

        public async Task<float[]> GenerateImageEmbeddingAsync(
     byte[] imageBytes,
     CancellationToken cancellationToken = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return Array.Empty<float>();
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("Gemini API Key is missing.");
            }

            if (string.IsNullOrWhiteSpace(_options.TextModel))
            {
                throw new InvalidOperationException("Gemini TextModel is missing.");
            }

            var caption = await GeminiVisionHelper.CaptionImageAsync(
                _httpClient,
                _options.ApiKey,
                _options.TextModel,
                imageBytes,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(caption))
            {
                return Array.Empty<float>();
            }

            return await GenerateEmbeddingAsync(
                caption,
                cancellationToken);
        }

        #region Response Models

        private sealed class GeminiEmbedResponse
        {
            [JsonPropertyName("embedding")]
            public GeminiEmbeddingValues? Embedding { get; set; }
        }

        private sealed class GeminiEmbeddingValues
        {
            [JsonPropertyName("values")]
            public double[]? Values { get; set; }
        }

        #endregion
    }
}
