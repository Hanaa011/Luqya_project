using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Providers
{
    internal static class OllamaVisionHelper
    {
        public static async Task<string> CaptionImageAsync(HttpClient httpClient, string visionModel, byte[] imageBytes, CancellationToken cancellationToken)
        {
            return await GenerateAsync(
                httpClient,
                visionModel,
                "Describe this object in one short sentence (type, color, brand if visible).",
                imageBytes,
                cancellationToken
            );
        }

        public static async Task<string> GenerateAsync(HttpClient httpClient, string model, string prompt, byte[]? imageBytes, CancellationToken cancellationToken)
        {
            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Images = imageBytes != null ? new[] { Convert.ToBase64String(imageBytes) } : null
            };

            var response = await httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            return result?.Response ?? string.Empty;
        }

        private class OllamaGenerateRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
            [JsonPropertyName("stream")] public bool Stream { get; set; }
            [JsonPropertyName("images")] public string[]? Images { get; set; }
        }

        private class OllamaGenerateResponse
        {
            [JsonPropertyName("response")] public string? Response { get; set; }
        }
    }
}
