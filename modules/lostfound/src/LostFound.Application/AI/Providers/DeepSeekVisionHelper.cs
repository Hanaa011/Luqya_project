using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Providers
{
    // NOTE: DeepSeek's public API is chat-completions only (OpenAI-compatible
    // /chat/completions), with no widely available/documented image-input
    // support at the time this was written. Named "VisionHelper" only for
    // naming parity with the other providers' helpers - imageBytes is
    // accepted for interface symmetry but intentionally ignored. If DeepSeek
    // adds public vision support later, this is the one place to wire it in.
    internal static class DeepSeekVisionHelper
    {
        public static async Task<string> GenerateAsync(
            HttpClient httpClient,
            string model,
            string prompt,
            byte[]? imageBytes,
            CancellationToken cancellationToken)
        {
            // imageBytes intentionally unused - see note above.
            var response = await httpClient.PostAsJsonAsync(
                "chat/completions",
                new
                {
                    model,
                    messages = new[] { new { role = "user", content = prompt } },
                    temperature = 0.2
                },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DeepSeekChatResponse>(cancellationToken: cancellationToken);
            return result?.Choices?.Length > 0 ? result.Choices[0].Message?.Content ?? string.Empty : string.Empty;
        }

        private sealed class DeepSeekChatResponse
        {
            [JsonPropertyName("choices")] public DeepSeekChoice[]? Choices { get; set; }
        }

        private sealed class DeepSeekChoice
        {
            [JsonPropertyName("message")] public DeepSeekMessage? Message { get; set; }
        }

        private sealed class DeepSeekMessage
        {
            [JsonPropertyName("content")] public string? Content { get; set; }
        }
    }
}
