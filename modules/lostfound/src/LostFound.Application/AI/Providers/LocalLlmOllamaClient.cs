using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Talks to a local Ollama daemon's <c>/api/generate</c> endpoint for
    /// <see cref="LocalLlmClassificationProvider"/>. Deliberately separate
    /// from <see cref="OllamaVisionHelper"/> (used by the remote/pluggable
    /// <see cref="OllamaClassificationProvider"/>): this client always
    /// requests <c>format:"json"</c> and <c>temperature:0</c> - required for
    /// the structured-JSON-only, deterministic behavior PHASE-VALIDATION-08
    /// specifically measured and selected a model against - and applies its
    /// own short, configurable timeout (<see cref="LocalLlmOptions.TimeoutSeconds"/>)
    /// so an unreachable/hung local daemon fails fast into the rule-based
    /// fallback rather than blocking the caller for HttpClient's default
    /// timeout.
    /// </summary>
    internal static class LocalLlmOllamaClient
    {
        public static Task<string> GenerateAsync(
            HttpClient httpClient,
            string baseUrl,
            string model,
            string prompt,
            int timeoutSeconds,
            CancellationToken cancellationToken) =>
            SendAsync(httpClient, baseUrl, model, prompt, images: null, jsonFormat: true, timeoutSeconds, cancellationToken);

        /// <summary>
        /// Vision-component call (paired-architecture - see the report's
        /// Multimodal Architecture Recommendation): asks a vision-capable
        /// model to describe the image in free text, NOT structured JSON -
        /// every vision-capable model tested here (moondream) followed a
        /// short free-text captioning instruction far more reliably than a
        /// JSON schema, and the caption is only ever fed back in as
        /// additional CONTEXT for the text model's own JSON response (see
        /// <see cref="LocalLlmClassificationPromptBuilder.Build"/>), so nothing
        /// downstream ever parses this call's output as JSON.
        /// </summary>
        public static Task<string> CaptionImageAsync(
            HttpClient httpClient,
            string baseUrl,
            string visionModel,
            byte[] imageBytes,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            // PHASE-VALIDATION-08 finding (see the report's Prompt
            // Engineering / Image Evaluation sections): moondream - and,
            // by extension, VLMs in its size class (~1.8B, caption-
            // specialized) - silently returns an empty response
            // (eval_count=1, an immediate stop token) for a longer,
            // multi-instruction captioning prompt, even though the SAME
            // image captions correctly with a short, single-instruction
            // one. This is not a flaky/occasional failure - it reproduced
            // on every longer-prompt attempt in testing. Kept short and
            // single-purpose deliberately; do not add more instructions to
            // this prompt without re-validating against a real model.
            const string captionPrompt = "Describe this object in one short sentence.";

            return SendAsync(
                httpClient, baseUrl, visionModel, captionPrompt,
                images: new[] { Convert.ToBase64String(imageBytes) },
                jsonFormat: false, timeoutSeconds, cancellationToken);
        }

        private static async Task<string> SendAsync(
            HttpClient httpClient,
            string baseUrl,
            string model,
            string prompt,
            string[]? images,
            bool jsonFormat,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var request = new GenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Format = jsonFormat ? "json" : null,
                Images = images,
                Options = new GenerateOptions { Temperature = 0 }
            };

            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/generate", request, linkedCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new LocalLlmException(
                    $"Local LLM request to model '{model}' timed out after {timeoutSeconds}s.");
            }
            catch (HttpRequestException ex)
            {
                throw new LocalLlmException(
                    $"Local LLM request to model '{model}' at '{baseUrl}' failed - is the Ollama daemon running? {ex.Message}",
                    ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LocalLlmException(
                    $"Local LLM request to model '{model}' failed with status {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(cancellationToken: linkedCts.Token);

            if (string.IsNullOrWhiteSpace(result?.Response))
            {
                throw new LocalLlmException($"Local LLM model '{model}' returned an empty response.");
            }

            return result.Response;
        }

        private sealed class GenerateRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
            [JsonPropertyName("stream")] public bool Stream { get; set; }
            [JsonPropertyName("format")] public string? Format { get; set; }
            [JsonPropertyName("images")] public string[]? Images { get; set; }
            [JsonPropertyName("options")] public GenerateOptions? Options { get; set; }
        }

        private sealed class GenerateOptions
        {
            [JsonPropertyName("temperature")] public double Temperature { get; set; }
        }

        private sealed class GenerateResponse
        {
            [JsonPropertyName("response")] public string? Response { get; set; }
        }
    }

    /// <summary>
    /// A local LLM call failure (daemon unreachable, timeout, non-success
    /// status, empty response). Always caught by
    /// <see cref="LocalLlmClassificationProvider"/> itself and turned into a
    /// fallback to the rule-based engine - never allowed to propagate out of
    /// <c>ClassifyAsync</c>, consistent with the local-first tier's "never
    /// hard-fail" contract (see <see cref="LostFound.AI.Core.ClassificationEngine"/>).
    /// </summary>
    internal sealed class LocalLlmException : Exception
    {
        public LocalLlmException(string message, Exception? innerException = null) : base(message, innerException)
        {
        }
    }
}
