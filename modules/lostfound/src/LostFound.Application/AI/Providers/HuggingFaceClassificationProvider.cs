using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    // ============================================================================
    // HONEST LIMITATION - please read before relying on this for production
    // ----------------------------------------------------------------------------
    // Unlike Gemini/OpenAI/Ollama/DeepSeek, HuggingFace's free inference API is
    // built around single-purpose models (zero-shot classification, image
    // captioning), not a flexible instruction-following chat model. To get the
    // same rich searchText/explanation/searchReason/searchKeywords output, this
    // provider now ALSO tries a hosted instruct model (HuggingFaceOptions.InstructModel,
    // default "HuggingFaceH4/zephyr-7b-beta") with the same shared prompt everyone
    // else uses. Free-tier instruct models can be slow to "cold start", rate
    // limited, or occasionally return non-JSON text despite instructions - if
    // that generative attempt fails or returns nothing usable, this provider
    // falls back to the ORIGINAL caption + zero-shot-category approach (no
    // objectType/color/brand/searchText - CategoryName only). This is a real
    // quality gap versus the other providers; it is not fully closed, only
    // best-effort narrowed.
    // ============================================================================
    public class HuggingFaceClassificationProvider : IItemClassificationProvider
    {
        private static readonly string[] CandidateCategories =
        {
            "Electronics", "Bags", "Documents", "Jewelry", "Clothing", "Keys", "Wallets", "Toys", "Other"
        };

        private readonly HttpClient _httpClient;
        private readonly HuggingFaceOptions _options;

        public string ProviderName => "HuggingFace";

        public HuggingFaceClassificationProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.HuggingFace;
        }

        public async Task<ItemClassificationResult> ClassifyAsync(string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            string? imageCaption = null;

            if (imageBytes != null)
            {
                imageCaption = await TryCaptionImageAsync(imageBytes, cancellationToken);
            }

            var textForClassification = string.IsNullOrWhiteSpace(description)
                ? imageCaption
                : (string.IsNullOrWhiteSpace(imageCaption) ? description : $"{description}. {imageCaption}");

            if (string.IsNullOrWhiteSpace(textForClassification))
            {
                return new ItemClassificationResult { CategoryName = "Uncategorized" };
            }

            // Best-effort: try the rich, shared-prompt generative path first.
            var generated = await TryGenerativeClassificationAsync(textForClassification, cancellationToken);
            if (generated != null)
            {
                generated.ImageCaption ??= imageCaption;
                return generated;
            }

            // Fall back to the original, more limited approach: image caption
            // (already have it) + zero-shot category label only.
            var fallback = new ItemClassificationResult
            {
                ImageCaption = imageCaption,
                CategoryName = await TryZeroShotCategoryAsync(textForClassification, cancellationToken) ?? "Other"
            };

            return fallback;
        }

        /// <summary>
        /// Attempts the same rich, JSON-structured classification every other
        /// provider does, via a hosted instruct model. Returns null (never
        /// throws) if the model fails to respond, is still loading, or
        /// returns nothing parseable - callers should fall back gracefully.
        /// </summary>
        private async Task<ItemClassificationResult?> TryGenerativeClassificationAsync(string text, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.InstructModel))
            {
                return null;
            }

            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://api-inference.huggingface.co/models/{_options.InstructModel}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                request.Content = JsonContent.Create(new
                {
                    inputs = ClassificationPromptBuilder.Build(text),
                    parameters = new { max_new_tokens = 400, return_full_text = false },
                    options = new { wait_for_model = true }
                });

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<HfTextGenerationResult[]>(cancellationToken: cancellationToken);
                var raw = result?.Length > 0 ? result[0].GeneratedText : null;

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var parsed = ClassificationJsonParser.Parse(raw);

                // ClassificationJsonParser never throws - it returns a bare
                // "Uncategorized" result on failure. Treat that as "nothing
                // usable" so the caller falls back instead of returning an
                // empty-looking success.
                return parsed.ObjectType != null || parsed.SearchText != null
                    ? parsed
                    : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The original zero-shot category classification, kept as a fallback
        /// for when the generative attempt above isn't available or fails.
        /// </summary>
        private async Task<string?> TryZeroShotCategoryAsync(string text, CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api-inference.huggingface.co/models/facebook/bart-large-mnli");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                request.Content = JsonContent.Create(new { inputs = text, parameters = new { candidate_labels = CandidateCategories } });

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<HfZeroShotResult>(cancellationToken: cancellationToken);
                return result?.Labels?.Length > 0 ? result.Labels[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> TryCaptionImageAsync(byte[] imageBytes, CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-inference.huggingface.co/models/{_options.ImageCaptionModel}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                request.Content = new ByteArrayContent(imageBytes);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<HfTextGenerationResult[]>(cancellationToken: cancellationToken);
                return result?.Length > 0 ? result[0].GeneratedText : null;
            }
            catch
            {
                return null;
            }
        }

        private class HfTextGenerationResult
        {
            [JsonPropertyName("generated_text")]
            public string? GeneratedText { get; set; }
        }

        private class HfZeroShotResult
        {
            public string[]? Labels { get; set; }
        }
    }
}
