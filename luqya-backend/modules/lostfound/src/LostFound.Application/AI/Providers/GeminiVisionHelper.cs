using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Providers
{
    internal static class GeminiVisionHelper
    {
        private const string BaseUrl =
            "https://generativelanguage.googleapis.com/v1beta/models";

        public static async Task<string> GenerateTextAsync(
            HttpClient httpClient,
            string apiKey,
            string model,
            string prompt,
            byte[]? imageBytes,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Gemini API Key is missing.");
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException("Gemini model is missing.");
            }

            var parts = new List<object>
            {
                new
                {
                    text = prompt
                }
            };

            if (imageBytes != null && imageBytes.Length > 0)
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = "image/jpeg",
                        data = Convert.ToBase64String(imageBytes)
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = parts
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2
                }
            };

            var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";

            var response = await httpClient.PostAsJsonAsync(
                url,
                requestBody,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Gemini request failed.\n" +
                    $"Status : {(int)response.StatusCode} ({response.StatusCode})\n\n" +
                    body);
            }

            var result = JsonSerializer.Deserialize<GenerateContentResponse>(
                body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result?
                       .Candidates?
                       .FirstOrDefault()?
                       .Content?
                       .Parts?
                       .FirstOrDefault()?
                       .Text?
                       .Trim()
                   ?? string.Empty;
        }

        public static Task<string> CaptionImageAsync(
     HttpClient httpClient,
     string apiKey,
     string model,
     byte[] imageBytes,
     CancellationToken cancellationToken)
        {
            return GenerateTextAsync(
                httpClient,
                apiKey,
                model,
                "Describe this object in one short sentence. Mention the object type, color, material and brand if visible.",
                imageBytes,
                cancellationToken);
        }

        #region Response Models

        private sealed class GenerateContentResponse
        {
            [JsonPropertyName("candidates")]
            public List<Candidate>? Candidates { get; set; }
        }

        private sealed class Candidate
        {
            [JsonPropertyName("content")]
            public CandidateContent? Content { get; set; }
        }

        private sealed class CandidateContent
        {
            [JsonPropertyName("parts")]
            public List<Part>? Parts { get; set; }
        }

        private sealed class Part
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }

        #endregion
    }
}