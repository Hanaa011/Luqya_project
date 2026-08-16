using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Talks to the Gemini <c>generateContent</c> endpoint. Public API
    /// (<see cref="GenerateTextAsync"/>, <see cref="CaptionImageAsync"/>) is
    /// unchanged from earlier versions of this file - request format, prompt
    /// format, response parsing format, image upload format, and the API
    /// endpoint are all untouched. What changed is reliability: transient
    /// failures (429/500/502/503/504, network errors, per-attempt timeouts)
    /// are now retried with exponential backoff instead of failing on the
    /// first hiccup, every attempt is bounded by a timeout, and failures
    /// raise a descriptive <see cref="GeminiApiException"/> instead of a bare
    /// <see cref="Exception"/>.
    /// </summary>
    internal static class GeminiVisionHelper
    {
        private const string BaseUrl =
            "https://generativelanguage.googleapis.com/v1beta/models";

        /// <summary>
        /// Total attempts (first try + retries) before giving up. Retry
        /// schedule: attempt 1 immediate, then wait 2s / 5s / 10s / 20s before
        /// attempts 2/3/4/5 respectively (see <see cref="GetBackoffDelay"/>).
        /// </summary>
        /// <remarks>
        /// Real E2E finding (Search&lt;-&gt;Matching Ranking Unification
        /// validation): the previous schedule (4 attempts, 1s/2s/4s backoff -
        /// under 8s total) was measured against real, sustained Gemini 429
        /// responses during a heavy real-dataset submission and NEVER once
        /// recovered - every single 429 exhausted all 4 attempts and fell
        /// through to the local classification tier, whose own accuracy for
        /// fine-grained categories (a 3B-parameter local LLM, or - failing
        /// that - a rule-based fallback) is measurably weaker than Gemini's
        /// (confirmed by re-running the exact same failed description
        /// through Gemini once the burst had passed: it classified correctly
        /// every time). Google's documented per-minute quota window is the
        /// most likely explanation - a sub-8-second retry budget can never
        /// outlast a 60-second quota reset. This does not change interactive
        /// search behavior: <see cref="LostFound.AiMatchingService"/>'s own
        /// 20s AiCallTimeout still cancels the whole call chain (including
        /// any in-progress backoff delay) at 20s regardless of this value, so
        /// only the background classification job - which has no such
        /// ceiling - benefits from the longer budget.
        /// </remarks>
        private const int MaxAttempts = 5;

        /// <summary>
        /// Per-attempt request timeout. Each of the <see cref="MaxAttempts"/>
        /// attempts gets its own fresh 20-second window - a single slow
        /// attempt doesn't eat into the budget of the next retry.
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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

            var requestBody = BuildRequestBody(prompt, imageBytes);
            var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";

            var responseBody = await SendWithRetryAsync(httpClient, url, requestBody, model, cancellationToken);

            return ExtractResponseText(responseBody, model);
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

        // =====================================================================
        // ---- Request building ---------------------------------------------
        // =====================================================================

        /// <summary>
        /// Builds the exact same request shape the previous version of this
        /// file sent - <c>contents[0].parts[]</c> (text, optionally followed
        /// by an <c>inline_data</c> image part) plus
        /// <c>generationConfig.temperature</c>. Extracted into its own method
        /// purely for readability; the JSON produced is unchanged.
        /// </summary>
        private static object BuildRequestBody(string prompt, byte[]? imageBytes)
        {
            var parts = new List<object> { new { text = prompt } };

            if (imageBytes is { Length: > 0 })
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

            return new
            {
                contents = new[] { new { parts } },
                generationConfig = new { temperature = 0.2 }
            };
        }

        // =====================================================================
        // ---- Retry / resiliency ---------------------------------------------
        // =====================================================================

        /// <summary>
        /// Sends the request, retrying up to <see cref="MaxAttempts"/> times
        /// total with exponential backoff (1s / 2s / 4s between attempts) for
        /// transient failures only:
        /// <list type="bullet">
        /// <item>HTTP 429, 500, 502, 503, 504 (see <see cref="IsTransientStatusCode"/>)</item>
        /// <item><see cref="HttpRequestException"/> (network-level failures)</item>
        /// <item>a per-attempt timeout (this method's own 20s window expiring)</item>
        /// </list>
        /// HTTP 400/401/403/404 (and any other non-transient status) fail
        /// immediately with no retry. If the CALLER's <paramref name="callerToken"/>
        /// is what actually got cancelled (as opposed to our own per-attempt
        /// timeout), that cancellation propagates immediately, unretried -
        /// the caller asked to stop, so we stop. On success, returns the raw
        /// response body for <see cref="ExtractResponseText"/> to parse
        /// exactly once. On exhausting all attempts, throws a
        /// <see cref="GeminiApiException"/> describing the last failure.
        /// </summary>
        private static async Task<string> SendWithRetryAsync(
            HttpClient httpClient,
            string url,
            object requestBody,
            string model,
            CancellationToken callerToken)
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                callerToken.ThrowIfCancellationRequested();

                using var timeoutCts = new CancellationTokenSource(RequestTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutCts.Token);
                var stopwatch = Stopwatch.StartNew();

                HttpResponseMessage response;
                string body;

                try
                {
                    // JsonContent.Create with the runtime type (rather than
                    // PostAsJsonAsync<object>) so the anonymous request body
                    // serializes by its actual shape, not as "{}".
                    using var content = JsonContent.Create(requestBody, requestBody.GetType());
                    response = await httpClient.PostAsync(url, content, linkedCts.Token);
                    body = await response.Content.ReadAsStringAsync(linkedCts.Token);
                }
                catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
                {
                    // Our own per-attempt timeout fired, not the caller's -
                    // treat like any other transient failure.
                    if (attempt == MaxAttempts)
                    {
                        throw new GeminiApiException(
                            $"Gemini request to model '{model}' timed out after {RequestTimeout.TotalSeconds:0}s " +
                            $"(attempt {attempt}/{MaxAttempts}, {stopwatch.ElapsedMilliseconds}ms elapsed).",
                            statusCode: null,
                            responseBody: null);
                    }

                    await Task.Delay(GetBackoffDelay(attempt), callerToken);
                    continue;
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == MaxAttempts)
                    {
                        throw new GeminiApiException(
                            $"Gemini request to model '{model}' failed due to a network error " +
                            $"(attempt {attempt}/{MaxAttempts}, {stopwatch.ElapsedMilliseconds}ms elapsed): {ex.Message}",
                            statusCode: null,
                            responseBody: null,
                            innerException: ex);
                    }

                    await Task.Delay(GetBackoffDelay(attempt), callerToken);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    return body;
                }

                if (!IsTransientStatusCode(response.StatusCode) || attempt == MaxAttempts)
                {
                    throw new GeminiApiException(
                        $"Gemini request to model '{model}' failed with status {(int)response.StatusCode} " +
                        $"({response.StatusCode}) (attempt {attempt}/{MaxAttempts}, {stopwatch.ElapsedMilliseconds}ms elapsed).",
                        response.StatusCode,
                        body);
                }

                await Task.Delay(GetBackoffDelay(attempt), callerToken);
            }

            // Unreachable - the loop above always either returns or throws -
            // present only to satisfy the compiler's flow analysis.
            throw new GeminiApiException(
                $"Gemini request to model '{model}' failed after {MaxAttempts} attempts.",
                statusCode: null,
                responseBody: null);
        }

        /// <summary>
        /// Delay to wait AFTER the given attempt number fails, before trying
        /// again: 2s / 5s / 10s / 20s after attempts 1-4 respectively (~37s
        /// total budget - see <see cref="MaxAttempts"/> remarks for why).
        /// There is no delay after the final attempt since no retry follows it.
        /// </summary>
        private static TimeSpan GetBackoffDelay(int attemptJustFailed) => attemptJustFailed switch
        {
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(5),
            3 => TimeSpan.FromSeconds(10),
            4 => TimeSpan.FromSeconds(20),
            _ => TimeSpan.Zero
        };

        /// <summary>
        /// HTTP statuses worth retrying: rate limiting and server-side
        /// transient failures. 400/401/403/404 and anything else fall
        /// through to "not transient" and fail immediately - retrying a bad
        /// request or bad credentials would never succeed.
        /// </summary>
        private static bool IsTransientStatusCode(HttpStatusCode statusCode) => statusCode switch
        {
            HttpStatusCode.TooManyRequests => true,     // 429
            HttpStatusCode.InternalServerError => true, // 500
            HttpStatusCode.BadGateway => true,           // 502
            HttpStatusCode.ServiceUnavailable => true,   // 503
            HttpStatusCode.GatewayTimeout => true,       // 504
            _ => false
        };

        // =====================================================================
        // ---- Response validation / parsing -----------------------------------
        // =====================================================================

        /// <summary>
        /// Parses the response body exactly once and safely extracts the
        /// generated text, validating every step along the way rather than
        /// assuming <c>candidates[0].content.parts[0].text</c> exists:
        /// checks for a JSON parse failure, a blocked prompt
        /// (<c>promptFeedback.blockReason</c>), no/empty candidates, and a
        /// missing/empty response text (in which case the candidate's
        /// <c>finishReason</c> - e.g. "SAFETY", "RECITATION" - is included in
        /// the exception if present, since that's usually the actual cause).
        /// </summary>
        private static string ExtractResponseText(string responseBody, string model)
        {
            GenerateContentResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GenerateContentResponse>(responseBody, SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new GeminiApiException(
                    $"Gemini returned a response for model '{model}' that could not be parsed as JSON.",
                    statusCode: null,
                    responseBody: responseBody,
                    innerException: ex);
            }

            if (!string.IsNullOrWhiteSpace(parsed?.PromptFeedback?.BlockReason))
            {
                throw new GeminiApiException(
                    $"Gemini blocked the prompt for model '{model}'. Reason: {parsed!.PromptFeedback!.BlockReason}.",
                    statusCode: null,
                    responseBody: responseBody);
            }

            var candidate = parsed?.Candidates?.FirstOrDefault();
            if (candidate == null)
            {
                throw new GeminiApiException(
                    $"Gemini returned no candidates for model '{model}'.",
                    statusCode: null,
                    responseBody: responseBody);
            }

            var text = candidate.Content?.Parts?.FirstOrDefault()?.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            var reasonSuffix = !string.IsNullOrWhiteSpace(candidate.FinishReason)
                ? $" Finish reason: {candidate.FinishReason}."
                : string.Empty;

            throw new GeminiApiException(
                $"Gemini returned an empty response for model '{model}'.{reasonSuffix}",
                statusCode: null,
                responseBody: responseBody);
        }

        #region Response Models

        private sealed class GenerateContentResponse
        {
            [JsonPropertyName("candidates")]
            public List<Candidate>? Candidates { get; set; }

            [JsonPropertyName("promptFeedback")]
            public PromptFeedback? PromptFeedback { get; set; }
        }

        private sealed class Candidate
        {
            [JsonPropertyName("content")]
            public CandidateContent? Content { get; set; }

            [JsonPropertyName("finishReason")]
            public string? FinishReason { get; set; }
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

        private sealed class PromptFeedback
        {
            [JsonPropertyName("blockReason")]
            public string? BlockReason { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Describes a Gemini request failure with enough detail to actually
    /// debug it: the HTTP status (when one was received), the raw response
    /// body (when available), and a message that already includes the
    /// model, attempt count, and elapsed time - <see cref="GeminiVisionHelper"/>
    /// deliberately takes no <c>ILogger</c> dependency, so this exception's
    /// message is the primary diagnostic surface.
    /// </summary>
    internal sealed class GeminiApiException : Exception
    {
        public HttpStatusCode? StatusCode { get; }
        public string? ResponseBody { get; }

        public GeminiApiException(
            string message,
            HttpStatusCode? statusCode,
            string? responseBody,
            Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}