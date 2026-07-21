using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    public class OllamaClassificationProvider : IItemClassificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;

        public string ProviderName => "Ollama";

        public OllamaClassificationProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.Ollama;
        }

        public async Task<ItemClassificationResult> ClassifyAsync(string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            var prompt =
                "Analyze this lost-and-found item (Arabic or English). Return ONLY JSON: " +
                "{\"category\":..,\"objectType\":..,\"color\":..,\"brand\":..,\"tags\":[..]}. " +
                $"Description: {description ?? "(none, use the image only)"}";

            var model = imageBytes != null ? _options.VisionModel : _options.Model;
            var raw = await OllamaVisionHelper.GenerateAsync(_httpClient, model, prompt, imageBytes, cancellationToken);

            return GeminiLikeJsonParser.Parse(raw);
        }
    }
}
