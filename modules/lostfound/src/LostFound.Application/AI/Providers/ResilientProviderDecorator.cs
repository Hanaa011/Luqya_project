using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Generalizes the retry/backoff pattern <see cref="GeminiVisionHelper"/>
    /// already implements internally (see its "Retry / resiliency" region) so
    /// EVERY provider can get the same resilience, not just Gemini. Unlike
    /// <see cref="GeminiVisionHelper"/>, this class has no knowledge of HTTP
    /// status codes - by the time a provider call reaches this layer, a
    /// transient failure has already surfaced as a plain
    /// <see cref="HttpRequestException"/> or a timeout-driven
    /// <see cref="OperationCanceledException"/>, which is exactly what this
    /// decorator retries on. Non-transient failures (bad request, bad
    /// credentials, parsing errors, etc.) propagate immediately, unretried.
    /// </summary>
    internal static class ResilientProviderDecorator
    {
        /// <summary>
        /// Delay to wait after the given attempt number fails, before trying
        /// again: 1s after attempt 1, 2s after attempt 2, 4s after any later
        /// attempt. Mirrors <c>GeminiVisionHelper.GetBackoffDelay</c>.
        /// </summary>
        private static TimeSpan GetBackoffDelay(int attemptJustFailed) => attemptJustFailed switch
        {
            1 => TimeSpan.FromSeconds(1),
            2 => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(4)
        };

        /// <summary>
        /// Runs <paramref name="operation"/>, retrying up to
        /// <paramref name="maxAttempts"/> times total (with exponential
        /// backoff between attempts) when it fails with a transient
        /// exception. The final attempt is unguarded: whatever it throws
        /// propagates straight to the caller, exactly as an unwrapped
        /// provider call would have. If the caller's own
        /// <paramref name="cancellationToken"/> is what actually triggered a
        /// cancellation (as opposed to the provider's own internal timeout),
        /// that propagates immediately, unretried.
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationDescription,
            ILogger logger,
            CancellationToken cancellationToken,
            int maxAttempts = 3)
        {
            for (var attempt = 1; attempt < maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await operation(cancellationToken);
                }
                catch (Exception ex) when (IsTransient(ex, cancellationToken))
                {
                    var delay = GetBackoffDelay(attempt);

                    logger.LogWarning(
                        ex,
                        "{Operation} failed on attempt {Attempt}/{MaxAttempts}; retrying in {Delay}.",
                        operationDescription,
                        attempt,
                        maxAttempts,
                        delay);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            // Final attempt: no retry budget left, let any failure propagate
            // to the caller exactly as an unwrapped provider call would.
            return await operation(cancellationToken);
        }

        /// <summary>
        /// A failure is worth retrying only when it looks like a transient
        /// network/timeout hiccup rather than a genuine, repeatable error
        /// (bad request, bad credentials, malformed response, etc.) or the
        /// caller itself asking to stop.
        /// </summary>
        private static bool IsTransient(Exception ex, CancellationToken callerToken) => ex switch
        {
            HttpRequestException => true,
            TaskCanceledException => !callerToken.IsCancellationRequested,
            OperationCanceledException => !callerToken.IsCancellationRequested,
            _ => false
        };
    }
}
