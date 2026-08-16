using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    public class HuggingFaceEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly HuggingFaceOptions _options;

        public string ProviderName => "HuggingFace";

        public HuggingFaceEmbeddingProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.HuggingFace;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-inference.huggingface.co/pipeline/feature-extraction/{_options.Model}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = JsonContent.Create(new { inputs = text, options = new { wait_for_model = true } });

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<float[]>(cancellationToken: cancellationToken);
            return result ?? Array.Empty<float>();
        }

        // Uses an image-captioning model (e.g. nlpconnect/vit-gpt2-image-captioning)
        // then embeds the resulting caption - same caption-then-embed
        // approach used by the other providers.
        public async Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-inference.huggingface.co/models/{_options.ImageCaptionModel}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new ByteArrayContent(imageBytes);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<HfCaptionResult[]>(cancellationToken: cancellationToken);
            var caption = result?.Length > 0 ? result[0].GeneratedText : string.Empty;

            return await GenerateEmbeddingAsync(caption ?? "item", cancellationToken);
        }

        private class HfCaptionResult
        {
            [JsonPropertyName("generated_text")]
            public string? GeneratedText { get; set; }
        }
    }
}
