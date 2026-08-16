using System.Collections.Generic;
using System.Text.Json;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Parses the shared classification JSON shape (category/objectType/color/
    /// brand/tags/explanation/searchReason/searchKeywords/searchText) that
    /// every provider's prompt (see <see cref="ClassificationPromptBuilder"/>)
    /// asks its model to return. Shared across all providers so parsing logic
    /// isn't duplicated per-provider. Tolerant of markdown code fences some
    /// models wrap JSON in, and degrades to an "Uncategorized" result rather
    /// than throwing if parsing fails for any reason.
    /// </summary>
    internal static class ClassificationJsonParser
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
                            : null,

                    // Display/debug-only metadata - never used for matching or embedding.
                    Explanation =
                        root.TryGetProperty("explanation", out var explanation)
                            ? explanation.GetString()
                            : null,

                    SearchReason =
                        root.TryGetProperty("searchReason", out var searchReason)
                            ? searchReason.GetString()
                            : null,

                    // The only field later embedded for semantic search - a
                    // natural-language paragraph, not a field concatenation.
                    SearchText =
                        root.TryGetProperty("searchText", out var searchText)
                            ? searchText.GetString()
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

                if (root.TryGetProperty("searchKeywords", out var searchKeywords) &&
                    searchKeywords.ValueKind == JsonValueKind.Array)
                {
                    result.SearchKeywords ??= new List<string>();
                    foreach (var keyword in searchKeywords.EnumerateArray())
                    {
                        var value = keyword.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            result.SearchKeywords.Add(value);
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
