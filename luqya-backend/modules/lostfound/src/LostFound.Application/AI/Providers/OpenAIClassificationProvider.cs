using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    public class OpenAIClassificationProvider : IItemClassificationProvider
    {
        private readonly HttpClient _httpClient;

        public string ProviderName => "OpenAI";

        public OpenAIClassificationProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            var apiKey = options.Value.OpenAI.ApiKey;
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<ItemClassificationResult> ClassifyAsync(string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            var prompt =
                "Analyze this lost-and-found item (Arabic or English). Return ONLY JSON: " +
                "{\"category\":..,\"objectType\":..,\"color\":..,\"brand\":..,\"tags\":[..]}. " +
                $"Description: {description ?? "(none, use the image only)"}";

            var raw = await OpenAIVisionHelper.GenerateAsync(_httpClient, prompt, imageBytes, cancellationToken);
            return GeminiLikeJsonParser.Parse(raw);
        }
    }
}
