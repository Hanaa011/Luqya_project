using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    // HF's free inference API is best at single-purpose models, not
    // flexible JSON-structured chat. This uses zero-shot classification
    // against a fixed label set for Category, plus the image caption (if
    // any) for the remaining free-text fields.
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
            var result = new ItemClassificationResult();
            var textForClassification = description;

            if (imageBytes != null)
            {
                var captionRequest = new HttpRequestMessage(HttpMethod.Post, $"https://api-inference.huggingface.co/models/{_options.ImageCaptionModel}");
                captionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                captionRequest.Content = new ByteArrayContent(imageBytes);

                var captionResponse = await _httpClient.SendAsync(captionRequest, cancellationToken);
                if (captionResponse.IsSuccessStatusCode)
                {
                    var caption = await captionResponse.Content.ReadFromJsonAsync<HfCaptionResult[]>(cancellationToken: cancellationToken);
                    if (caption?.Length > 0)
                    {
                        result.ImageCaption = caption[0].GeneratedText;
                        textForClassification = string.IsNullOrWhiteSpace(textForClassification)
                            ? result.ImageCaption
                            : $"{textForClassification}. {result.ImageCaption}";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(textForClassification))
            {
                result.CategoryName = "Uncategorized";
                return result;
            }

            var zeroShotRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-inference.huggingface.co/models/facebook/bart-large-mnli");
            zeroShotRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            zeroShotRequest.Content = JsonContent.Create(new { inputs = textForClassification, parameters = new { candidate_labels = CandidateCategories } });

            var zeroShotResponse = await _httpClient.SendAsync(zeroShotRequest, cancellationToken);
            if (zeroShotResponse.IsSuccessStatusCode)
            {
                var zeroShot = await zeroShotResponse.Content.ReadFromJsonAsync<HfZeroShotResult>(cancellationToken: cancellationToken);
                result.CategoryName = zeroShot?.Labels?.Length > 0 ? zeroShot.Labels[0] : "Other";
            }
            else
            {
                result.CategoryName = "Other";
            }

            return result;
        }

        private class HfCaptionResult
        {
            public string? GeneratedText { get; set; }
        }

        private class HfZeroShotResult
        {
            public string[]? Labels { get; set; }
        }
    }
}
