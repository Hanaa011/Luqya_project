using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LostFound.AI.Providers;

namespace LostFound.AI.AiService
{
    public class AiServiceClient : IAiServiceClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;
        private readonly ILogger<AiServiceClient> _logger;

        public AiServiceClient(HttpClient httpClient, IOptions<AiServiceOptions> options, ILogger<AiServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
            _httpClient.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        }

        public Task<AiServiceSearchResult> SearchTextAsync(
            string text, string? locationName, bool isFinder,
            (string? Type, string? Description, string? Color, string? Location, string? ReportKind, string? ItemNameLocal) knownContext,
            CancellationToken cancellationToken = default)
        {
            return ResilientProviderDecorator.ExecuteAsync(
                async ct =>
                {
                    using var content = new MultipartFormDataContent
                    {
                        { new StringContent(text), "message" },
                        { new StringContent(isFinder ? "found" : "lost"), "report_kind" },
                    };
                    if (!string.IsNullOrWhiteSpace(locationName))
                    {
                        content.Add(new StringContent(locationName), "location_name");
                    }
                    AddKnownContext(content, knownContext);

                    var response = await PostAsync("api/ai/chat-search", content, ct);
                    return ToSearchResult(response);
                },
                "ai_service chat-search",
                _logger,
                cancellationToken,
                // Root-caused latency fix: a timeout here almost never means
                // "transient network blip" (the default this decorator was
                // written for) - chat-search's own work (LLM extraction +
                // report fetch) is naturally slower and more variable than a
                // typical provider call, so a timeout usually just means
                // "still legitimately working." Retrying doesn't make that
                // underlying work faster - it only piles another full
                // attempt's wait on top, which measured as a large chunk of
                // the reported "close to a minute" cases. One attempt, sized
                // by a realistic TimeoutSeconds (see AiServiceOptions),
                // avoids that compounding wait entirely.
                maxAttempts: 1);
        }

        public Task<AiServiceSearchResult> SearchImageAsync(
            byte[] imageBytes, string mimeType, string? text, string? locationName, bool isFinder,
            (string? Type, string? Description, string? Color, string? Location, string? ReportKind, string? ItemNameLocal) knownContext,
            CancellationToken cancellationToken = default)
        {
            return ResilientProviderDecorator.ExecuteAsync(
                async ct =>
                {
                    using var content = new MultipartFormDataContent
                    {
                        { CreateImageContent(imageBytes, mimeType), "image", "image" },
                        { new StringContent(isFinder ? "found" : "lost"), "report_kind" },
                    };
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        content.Add(new StringContent(text), "message");
                    }
                    if (!string.IsNullOrWhiteSpace(locationName))
                    {
                        content.Add(new StringContent(locationName), "location_name");
                    }
                    AddKnownContext(content, knownContext);

                    var response = await PostAsync("api/ai/match-image", content, ct);
                    return ToSearchResult(response);
                },
                "ai_service match-image",
                _logger,
                cancellationToken,
                // Same reasoning as SearchTextAsync - image search (vision +
                // extraction + report fetch) is the single slowest call this
                // client makes; retrying a timeout here only compounds the
                // wait for no benefit.
                maxAttempts: 1);
        }

        public Task<AiImageAnalysisResult> AnalyzeImageAsync(
            byte[] imageBytes, string mimeType, CancellationToken cancellationToken = default)
        {
            return ResilientProviderDecorator.ExecuteAsync(
                async ct =>
                {
                    using var content = new MultipartFormDataContent
                    {
                        { CreateImageContent(imageBytes, mimeType), "image", "image" },
                    };

                    using var httpResponse = await _httpClient.PostAsync("api/ai/analyze-image", content, ct);
                    httpResponse.EnsureSuccessStatusCode();

                    var json = await httpResponse.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<AiImageAnalysisResult>(json, JsonOptions);

                    return result ?? new AiImageAnalysisResult();
                },
                "ai_service analyze-image",
                _logger,
                cancellationToken);
        }

        private static void AddKnownContext(
            MultipartFormDataContent content,
            (string? Type, string? Description, string? Color, string? Location, string? ReportKind, string? ItemNameLocal) knownContext)
        {
            if (!string.IsNullOrWhiteSpace(knownContext.Type))
            {
                content.Add(new StringContent(knownContext.Type), "context_type");
            }
            if (!string.IsNullOrWhiteSpace(knownContext.Description))
            {
                content.Add(new StringContent(knownContext.Description), "context_description");
            }
            if (!string.IsNullOrWhiteSpace(knownContext.Color))
            {
                content.Add(new StringContent(knownContext.Color), "context_color");
            }
            if (!string.IsNullOrWhiteSpace(knownContext.Location))
            {
                content.Add(new StringContent(knownContext.Location), "context_location");
            }
            if (!string.IsNullOrWhiteSpace(knownContext.ReportKind))
            {
                content.Add(new StringContent(knownContext.ReportKind), "context_report_kind");
            }
            if (!string.IsNullOrWhiteSpace(knownContext.ItemNameLocal))
            {
                content.Add(new StringContent(knownContext.ItemNameLocal), "context_item_name_local");
            }
        }

        private static AiServiceSearchResult ToSearchResult(AiSearchResponseBody body) => new()
        {
            Reply = body.Reply,
            ShouldMatch = body.ShouldMatch,
            ExtractedType = body.ExtractedItem?.Type,
            ExtractedDescription = body.ExtractedItem?.Description,
            ExtractedColor = body.ExtractedItem?.Color,
            ExtractedLocation = body.ExtractedItem?.Location,
            ReportKind = body.ReportKind,
            ItemNameLocal = body.ItemNameLocal,
            FollowUpPrompt = body.FollowUpPrompt,
            Matches = body.Matches ?? new List<AiServiceMatch>(),
        };

        private async Task<AiSearchResponseBody> PostAsync(string path, HttpContent content, CancellationToken cancellationToken)
        {
            using var httpResponse = await _httpClient.PostAsync(path, content, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var body = JsonSerializer.Deserialize<AiSearchResponseBody>(json, JsonOptions);

            if (body == null)
            {
                throw new HttpRequestException("ai_service returned an empty response body.");
            }

            return body;
        }

        private static ByteArrayContent CreateImageContent(byte[] imageBytes, string mimeType)
        {
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
            return imageContent;
        }

        private class AiSearchResponseBody
        {
            [JsonPropertyName("reply")]
            public string? Reply { get; set; }

            [JsonPropertyName("should_match")]
            public bool ShouldMatch { get; set; }

            [JsonPropertyName("extracted_item")]
            public ExtractedItemBody? ExtractedItem { get; set; }

            [JsonPropertyName("report_kind")]
            public string? ReportKind { get; set; }

            [JsonPropertyName("item_name_local")]
            public string? ItemNameLocal { get; set; }

            [JsonPropertyName("follow_up_prompt")]
            public string? FollowUpPrompt { get; set; }

            [JsonPropertyName("matches")]
            public List<AiServiceMatch>? Matches { get; set; }
        }

        private class ExtractedItemBody
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("color")]
            public string? Color { get; set; }

            [JsonPropertyName("location")]
            public string? Location { get; set; }
        }
    }
}
