using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    public class OllamaEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;

        public string ProviderName => "Ollama";

        public OllamaEmbeddingProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.Ollama;
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/embeddings",
                new OllamaEmbeddingRequest { Model = _options.Model, Prompt = text },
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
            return result?.Embedding ?? Array.Empty<float>();
        }

        // Requires a vision-capable local model (e.g. `ollama pull llava`)
        // set via LostFound:AI:Ollama:VisionModel - captions the image, then
        // embeds the caption with the text model above.
        public async Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            var caption = await OllamaVisionHelper.CaptionImageAsync(_httpClient, _options.VisionModel, imageBytes, cancellationToken);
            return await GenerateEmbeddingAsync(caption, cancellationToken);
        }

        private class OllamaEmbeddingRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
        }

        private class OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")] public float[]? Embedding { get; set; }
        }
    }
}
