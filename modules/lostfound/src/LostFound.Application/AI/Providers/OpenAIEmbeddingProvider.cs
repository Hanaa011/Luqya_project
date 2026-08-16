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
    public class OpenAIEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAIOptions _options;

        public string ProviderName => "OpenAI";

        public OpenAIEmbeddingProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.OpenAI;
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "embeddings",
                new OpenAIEmbeddingRequest { Model = _options.Model, Input = text },
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(cancellationToken: cancellationToken);
            return result?.Data?.Length > 0 ? result.Data[0].Embedding : Array.Empty<float>();
        }

        // Caption via GPT-4o-mini vision, then embed the caption.
        public async Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            var caption = await OpenAIVisionHelper.CaptionImageAsync(_httpClient, imageBytes, cancellationToken);
            return await GenerateEmbeddingAsync(caption, cancellationToken);
        }

        private class OpenAIEmbeddingRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("input")] public string Input { get; set; } = string.Empty;
        }

        private class OpenAIEmbeddingResponse
        {
            [JsonPropertyName("data")] public OpenAIEmbeddingData[]? Data { get; set; }
        }

        private class OpenAIEmbeddingData
        {
            [JsonPropertyName("embedding")] public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }

    internal static class OpenAIVisionHelper
    {
        public static async Task<string> CaptionImageAsync(HttpClient httpClient, byte[] imageBytes, CancellationToken cancellationToken)
        {
            return await GenerateAsync(
                httpClient,
                "Describe this object in one short sentence (type, color, brand if visible).",
                imageBytes,
                cancellationToken
            );
        }

        public static async Task<string> GenerateAsync(HttpClient httpClient, string prompt, byte[]? imageBytes, CancellationToken cancellationToken)
        {
            var content = new System.Collections.Generic.List<object> { new { type = "text", text = prompt } };

            if (imageBytes != null)
            {
                content.Add(new
                {
                    type = "image_url",
                    image_url = new { url = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}" }
                });
            }

            var response = await httpClient.PostAsJsonAsync(
                "chat/completions",
                new { model = "gpt-4o-mini", messages = new[] { new { role = "user", content } } },
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(cancellationToken: cancellationToken);
            return result?.Choices?.Length > 0 ? result.Choices[0].Message?.Content ?? string.Empty : string.Empty;
        }

        private class OpenAIChatResponse
        {
            [JsonPropertyName("choices")] public OpenAIChoice[]? Choices { get; set; }
        }

        private class OpenAIChoice
        {
            [JsonPropertyName("message")] public OpenAIMessage? Message { get; set; }
        }

        private class OpenAIMessage
        {
            [JsonPropertyName("content")] public string? Content { get; set; }
        }
    }
}
