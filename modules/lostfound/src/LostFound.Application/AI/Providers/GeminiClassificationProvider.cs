using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    public class GeminiClassificationProvider : IItemClassificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;

        public string ProviderName => "Gemini";

        public GeminiClassificationProvider(
            HttpClient httpClient,
            IOptions<AIProviderOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value.Gemini
                ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<ItemClassificationResult> ClassifyAsync(
            string? description,
            byte[]? imageBytes,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("Gemini API Key is missing.");
            }

            if (string.IsNullOrWhiteSpace(_options.TextModel))
            {
                throw new InvalidOperationException("Gemini TextModel is missing.");
            }

            var prompt =
    "Analyze the following lost or found item.\n\n" +
    "Return ONLY valid JSON.\n" +
    "Do not return markdown.\n" +
    "Do not return explanations.\n\n" +
    "JSON format:\n\n" +
    "{\n" +
    "  \"category\": \"\",\n" +
    "  \"objectType\": \"\",\n" +
    "  \"color\": \"\",\n" +
    "  \"brand\": \"\",\n" +
    "  \"tags\": []\n" +
    "}\n\n" +
    "Description:\n" +
    (description ?? "(Image only)");

            var raw = await GeminiVisionHelper.GenerateTextAsync(
                _httpClient,
                _options.ApiKey,
                _options.TextModel,
                prompt,
                imageBytes,
                cancellationToken);

            return GeminiLikeJsonParser.Parse(raw);
        }
    }

    internal static class GeminiLikeJsonParser
    {
        public static ItemClassificationResult Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new ItemClassificationResult
                {
                    CategoryName = "Uncategorized"
                };
            }

            try
            {
                var cleaned = raw.Trim();

                if (cleaned.StartsWith("```"))
                {
                    cleaned = cleaned
                        .Replace("```json", "")
                        .Replace("```", "")
                        .Trim();
                }

                var start = cleaned.IndexOf('{');
                var end = cleaned.LastIndexOf('}');

                if (start >= 0 && end > start)
                {
                    cleaned = cleaned.Substring(start, end - start + 1);
                }

                using var document = JsonDocument.Parse(cleaned);

                var root = document.RootElement;

                var result = new ItemClassificationResult
                {
                    CategoryName =
                        root.TryGetProperty("category", out var category)
                            ? category.GetString() ?? "Uncategorized"
                            : "Uncategorized",

                    ObjectType =
                        root.TryGetProperty("objectType", out var objectType)
                            ? objectType.GetString()
                            : null,

                    Color =
                        root.TryGetProperty("color", out var color)
                            ? color.GetString()
                            : null,

                    Brand =
                        root.TryGetProperty("brand", out var brand)
                            ? brand.GetString()
                            : null
                };

                if (root.TryGetProperty("tags", out var tags) &&
                    tags.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tags.EnumerateArray())
                    {
                        var value = tag.GetString();

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            result.Tags.Add(value);
                        }
                    }
                }

                return result;
            }
            catch
            {
                return new ItemClassificationResult
                {
                    CategoryName = "Uncategorized"
                };
            }
        }
    }
}