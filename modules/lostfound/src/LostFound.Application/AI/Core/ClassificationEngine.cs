using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Core
{
    /// <summary>
    /// Default <see cref="IClassificationEngine"/>: tries an ordered chain of
    /// <see cref="IItemClassificationProvider"/> instances in turn - built by
    /// <see cref="LostFoundAiProvidersServiceCollectionExtensions.AddLostFoundSemanticAiFoundation"/>
    /// as [configured primary, Gemini] - falling through to the next only
    /// when the current one THROWS (timeout, HTTP failure, auth failure,
    /// quota/rate limit, provider unreachable, ...). A successful call is
    /// never re-tried on a later provider just because its content is sparse
    /// or a field is null - that is still a valid classification result and
    /// is returned as-is, exactly as the original single-provider design
    /// already did. If every remote provider in the chain fails, this falls
    /// through once more to <see cref="ILocalClassificationProvider"/> - the
    /// same Local-First posture <see cref="LostFound.AI.Embeddings.LocalFirstEmbeddingEngine"/>
    /// already upholds for embeddings. Only if the local fallback ALSO fails
    /// does this degrade to an empty <see cref="ItemClassificationResult"/>.
    ///
    /// This class has no provider-specific knowledge - WHICH providers are in
    /// the chain, and in what order, is entirely a DI-wiring decision (see
    /// the class referenced above). Adding another remote fallback tier
    /// later (e.g. a third external provider) is therefore a wiring change
    /// only; this file does not need to change.
    ///
    /// This is the single seam both <see cref="LostFound.AiMatchingService"/>
    /// and <see cref="LostFound.BackgroundJobs.ReportMatchingBackgroundJob"/>
    /// depend on, so the fallback chain applies uniformly to both callers.
    /// </summary>
    internal sealed class ClassificationEngine : IClassificationEngine
    {
        private readonly IReadOnlyList<IItemClassificationProvider> _remoteProviders;
        private readonly ILocalClassificationProvider _localProvider;
        private readonly ILogger<ClassificationEngine> _logger;

        public ClassificationEngine(
            IReadOnlyList<IItemClassificationProvider> remoteProviders,
            ILocalClassificationProvider localProvider,
            ILogger<ClassificationEngine> logger)
        {
            if (remoteProviders is null || remoteProviders.Count == 0)
            {
                throw new ArgumentException(
                    "At least one remote classification provider is required.", nameof(remoteProviders));
            }

            _remoteProviders = remoteProviders;
            _localProvider = localProvider;
            _logger = logger;
        }

        public async Task<ItemClassificationResult> ClassifyAsync(
            string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _remoteProviders.Count; i++)
            {
                var provider = _remoteProviders[i];

                try
                {
                    var result = await provider.ClassifyAsync(description, imageBytes, cancellationToken);

                    _logger.LogInformation(
                        "Classification produced by '{Provider}' (remote tier {Tier}/{TotalTiers}). " +
                        "Category={Category}, ObjectType={ObjectType}, Color={Color}, Brand={Brand}.",
                        provider.ProviderName, i + 1, _remoteProviders.Count,
                        result.CategoryName, result.ObjectType, result.Color, result.Brand);

                    return result;
                }
                catch (Exception ex) when (!IsCallerCancellation(ex, cancellationToken))
                {
                    var nextTier = i + 1 < _remoteProviders.Count
                        ? $"the next remote provider ('{_remoteProviders[i + 1].ProviderName}')"
                        : "the local fallback";

                    _logger.LogWarning(
                        ex,
                        "AI classification via '{Provider}' failed; automatically falling back to {NextTier} for this item.",
                        provider.ProviderName, nextTier);
                }
            }

            try
            {
                var localResult = await _localProvider.ClassifyAsync(description, imageBytes, cancellationToken);

                _logger.LogInformation(
                    "Classification produced by '{Provider}' (local fallback, after every remote provider failed). " +
                    "Category={Category}, ObjectType={ObjectType}, Color={Color}, Brand={Brand}.",
                    _localProvider.ProviderName, localResult.CategoryName, localResult.ObjectType,
                    localResult.Color, localResult.Brand);

                return localResult;
            }
            catch (Exception localEx) when (!IsCallerCancellation(localEx, cancellationToken))
            {
                _logger.LogWarning(
                    localEx,
                    "Local classification fallback also failed after every remote provider failed; continuing " +
                    "without AI classification for this item. Category/object type/color/brand/tags will be " +
                    "unavailable, but text-embedding-based matching is unaffected.");

                return new ItemClassificationResult();
            }
        }

        // Mirrors ResilientProviderDecorator.IsTransient's own caller-vs-
        // provider distinction: TaskCanceledException/OperationCanceledException
        // is also how an HttpClient timeout surfaces (not just an actual
        // caller-requested cancellation), so only treat it as "let it
        // propagate" when the caller's own token is the one that fired -
        // everything else, including a provider-side timeout, degrades.
        private static bool IsCallerCancellation(Exception ex, CancellationToken callerToken) =>
            ex is OperationCanceledException && callerToken.IsCancellationRequested;
    }
}
