using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// OpenAI-backed <see cref="IItemClassificationProvider"/>. Now uses the
    /// same shared <see cref="ClassificationPromptBuilder"/> as every other
    /// provider - previously this used a much simpler inline prompt that
    /// never asked for <c>searchText</c>/<c>explanation</c>/<c>searchReason</c>/
    /// <c>searchKeywords</c>, silently degrading match quality whenever this
    /// provider was selected. That gap is closed now.
    /// </summary>
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
            var prompt = ClassificationPromptBuilder.Build(description);

            var raw = await OpenAIVisionHelper.GenerateAsync(_httpClient, prompt, imageBytes, cancellationToken);
            return ClassificationJsonParser.Parse(raw);
        }
    }
}
