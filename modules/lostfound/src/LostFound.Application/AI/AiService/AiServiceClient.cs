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

        public Task<List<AiServiceMatch>> SearchTextAsync(
            string text, string? locationName, bool isFinder, CancellationToken cancellationToken = default)
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

                    var response = await PostAsync("api/ai/chat-search", content, ct);
                    return response.Matches ?? new List<AiServiceMatch>();
                },
                "ai_service chat-search",
                _logger,
                cancellationToken);
        }

        public Task<List<AiServiceMatch>> SearchImageAsync(
            byte[] imageBytes, string mimeType, string? text, string? locationName, bool isFinder,
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

                    var response = await PostAsync("api/ai/match-image", content, ct);
                    return response.Matches ?? new List<AiServiceMatch>();
                },
                "ai_service match-image",
                _logger,
                cancellationToken);
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

        // Only the fields this client actually reads - ai_service's response
        // carries more (reply/extracted_item/decision/etc.), never needed here.
        private class AiSearchResponseBody
        {
            [JsonPropertyName("matches")]
            public List<AiServiceMatch>? Matches { get; set; }
        }
    }
}
