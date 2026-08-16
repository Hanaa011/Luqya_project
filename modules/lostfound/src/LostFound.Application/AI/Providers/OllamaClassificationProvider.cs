using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Ollama-backed <see cref="IItemClassificationProvider"/> (local models).
    /// Now uses the shared <see cref="ClassificationPromptBuilder"/> - same
    /// note as <see cref="OpenAIClassificationProvider"/>: previously this
    /// used a much simpler inline prompt with no <c>searchText</c> guidance.
    /// Model quality/instruction-following varies a lot across local models,
    /// so <c>searchText</c> quality here depends on which model is configured
    /// via <c>Ollama:Model</c> / <c>Ollama:VisionModel</c> - a small model may
    /// not follow the detailed synonym/dialect guidance as reliably as
    /// Gemini/GPT-4o-mini do.
    /// </summary>
    public class OllamaClassificationProvider : IItemClassificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;

        public string ProviderName => "Ollama";

        public OllamaClassificationProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.Ollama;
            _httpClient.BaseAddress = new System.Uri(_options.BaseUrl);
        }

        public async Task<ItemClassificationResult> ClassifyAsync(string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            var prompt = ClassificationPromptBuilder.Build(description);

            var model = imageBytes != null ? _options.VisionModel : _options.Model;
            var raw = await OllamaVisionHelper.GenerateAsync(_httpClient, model, prompt, imageBytes, cancellationToken);

            return ClassificationJsonParser.Parse(raw);
        }
    }
}
