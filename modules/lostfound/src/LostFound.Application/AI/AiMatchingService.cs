using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using LostFound.AI.Concepts;
using LostFound.AI.Configuration;
using LostFound.AI.Core;
using LostFound.AI.Integration;
using LostFound.AI.Ontology;
using LostFound.Categories;
using LostFound.Reports;
using LostFound.Matching;

namespace LostFound.AI
{
    // NOTE ON CONTRACTS
    // ------------------------------------------------------------------
    // This service depends on the capability interfaces IEmbeddingEngine /
    // IClassificationEngine (LostFound.AI.Core), introduced in Phase 2A
    // Part 1 (Enterprise AI Foundation) as the seam between "what this
    // service needs" and "which provider currently supplies it". Both
    // interfaces are thin wrappers over the provider contracts below - see
    // LostFound.AI.Core.ClassificationEngine and
    // LostFound.AI.Embeddings.ProviderFallbackEmbeddingEngine - so the
    // underlying provider contract is still worth documenting here:
    //
    // IItemClassificationProvider / ItemClassificationResult (LostFound.AI):
    //
    //     public interface IItemClassificationProvider
    //     {
    //         string ProviderName { get; }
    //         Task<ItemClassificationResult> ClassifyAsync(
    //             string? description, byte[]? imageBytes, CancellationToken cancellationToken = default);
    //     }
    //
    //     public class ItemClassificationResult
    //     {
    //         public string? CategoryName { get; set; }
    //         public string? ObjectType { get; set; }
    //         public string? Color { get; set; }
    //         public string? Brand { get; set; }
    //         public string? Explanation { get; set; }
    //         public string? SearchReason { get; set; }
    //         public List<string> SearchKeywords { get; set; } = new();
    //         public string? SearchText { get; set; }   // <- the ONLY field embedded, see GenerateQueryEmbeddingsAsync
    //         public List<string> Tags { get; set; } = new();
    //         public string? ImageCaption { get; set; }
    //     }
    //
    // Explanation/SearchReason/SearchKeywords/ImageCaption are display/debug
    // metadata only - never read by this service. SearchText is the natural-
    // language paragraph the classification provider generates specifically
    // for embedding quality (see GeminiClassificationProvider.BuildPrompt);
    // this service must prefer it over building its own concatenation.
    //
    // MatchExplanationGenerator (see MatchExplanationGenerator.cs) is
    // dedicated to natural-language explanation ONLY. It never scores - it
    // describes a score that has already been finalized. It is a plain,
    // deterministic C# class: no HTTP calls, no AI model, no provider
    // abstraction - there used to be a second LLM round trip here
    // (IMatchExplanationProvider, one Gemini/OpenAI/etc. call per result)
    // purely to phrase a sentence out of facts the app already had; that
    // entire call has been removed in favor of building the sentence
    // locally, which is both instant and, being deterministic, perfectly
    // reproducible for the same inputs.
    // ------------------------------------------------------------------

    public class AiMatchingService : IAiMatchingService, ITransientDependency
    {
        /// <summary>
        /// All hybrid-scoring weights and bonuses in one place, so they can
        /// later be swapped for values pulled from <c>IConfiguration</c>
        /// without touching the scoring methods themselves.
        /// A full BASE match totals 100: Text(55) + Image(15) + ObjectType(15)
        /// + Color(5) + Brand(5) + Tag(5). On top of that, <see cref="CalculateDynamicBoosts"/>
        /// can add a further <see cref="MaxDynamicBoost"/> points for strong
        /// signals a flat weight can't capture (many shared tags, an exact
        /// model/color/brand mention in the free text) - see Task 5. Location/
        /// Date are reserved at 0 until their scoring rules are implemented.
        /// These weights are used ONLY by the deterministic scoring methods
        /// below - the explanation layer never sees or influences them.
        /// </summary>
        private static class ScoringWeights
        {
            public const double TextWeight = 0.55;
            public const double ImageWeight = 0.15;

            // Phase 3 Part 4: a distinct weight profile used ONLY when the
            // query has an image and no meaningful typed text (see
            // AiMatchingService.HasMeaningfulText). Live evidence against
            // real reports (SemanticReports/Phase-3-Part-4-*) showed the
            // default TextWeight/ImageWeight split badly miscalibrated for
            // this query shape: the "text" term for an image-only query is
            // never real user text - it's an embedding of the query image's
            // OWN AI-generated caption, compared against each candidate's
            // description embedding (a cross-modal proxy, not a direct
            // signal). Under the external embedding provider active in this
            // workspace (see ConceptResolver's EngineName remarks - no local
            // model has ever been installed here), that proxy has a high,
            // barely-discriminating noise floor: a completely unrelated
            // candidate and the genuine match both land in the low-80s
            // percent range. Meanwhile raw ImageScore - real image-cosine
            // similarity between the query photo and a candidate's own
            // photo - is far more discriminating in the same evidence (0 for
            // candidates with no image embedding, low-to-mid 60s for
            // unrelated-but-embedded candidates, low-to-mid 90s for genuine
            // matches). These two weights are a straight swap of the default
            // pair (same 0.70 combined budget) so the image-only path trusts
            // its one genuinely direct signal - the photo itself - as the
            // primary driver, with the cross-modal caption text kept only as
            // a secondary signal instead of the dominant one.
            public const double ImageOnlyTextWeight = 0.15;
            public const double ImageOnlyImageWeight = 0.55;

            public const double ObjectTypeBonus = 15;
            public const double ColorBonus = 5;
            public const double BrandBonus = 5;
            public const double TagBonus = 5;

            // Reserved for future rules - see CalculateLocationScore / CalculateDateScore.
            public const double LocationBonus = 0;
            public const double DateBonus = 0;

            // ---- Penalty tiers (Task 6) ----
            // A flat penalty for object-type disagreement isn't enough: a
            // Phone-vs-Wallet mismatch and a Phone-vs-Car mismatch are not
            // equally "wrong". See ObjectTypeRelationship for the clustering
            // that decides which tier applies.
            public const double UnknownObjectTypePenalty = -40;
            public const double RelatedClusterPenalty = -35;
            public const double UnrelatedClusterPenalty = -75;

            // ---- Dynamic boosts (Task 5) ----
            public const double MaxSharedTagsBoost = 10;
            public const double ExactModelMentionBoost = 5;
            public const double ExactColorMentionBoost = 3;
            public const double ExactBrandMentionBoost = 3;

            public const double MaxDynamicBoost =
                MaxSharedTagsBoost + ExactModelMentionBoost + ExactColorMentionBoost + ExactBrandMentionBoost;
        }

        /// <summary>
        /// Thresholds used purely to phrase the deterministic fallback
        /// explanation (e.g. "highly similar" vs "moderately similar"). They
        /// do not affect scoring and are not sent to the LLM.
        /// </summary>
        private static class ReasonThresholds
        {
            public const double High = 70;
            public const double Moderate = 40;
        }

        /// <summary>
        /// Maximum time to wait on the classification AI call before giving
        /// up and degrading gracefully. Gemini's own latency for this call
        /// has been observed at 8-12s, which is too slow/unpredictable for a
        /// search endpoint - capping it bounds worst-case response time at
        /// the cost of occasionally falling back to raw-text search instead
        /// of a real classification result. Embedding calls are NOT subject
        /// to this timeout - they've been consistently fast (&lt;1s) and are
        /// not optional the way classification is. Explanation generation no
        /// longer makes an AI call at all (see <see cref="MatchExplanationGenerator"/>),
        /// so it no longer needs a timeout either.
        /// </summary>
        private static readonly TimeSpan AiCallTimeout = TimeSpan.FromSeconds(20);

        private readonly IEmbeddingEngine _embeddingEngine;
        private readonly IClassificationEngine _classificationEngine;
        private readonly IReportRepository _reportRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IConceptResolver _conceptResolver;
        private readonly IObjectTypeCompatibilityService _objectTypeCompatibilityService;
        private readonly ISemanticSearchOrchestrator _semanticSearchOrchestrator;
        private readonly IOptions<HybridPipelineOptions> _hybridPipelineOptions;
        private readonly ILogger<AiMatchingService> _logger;

        public AiMatchingService(
            IEmbeddingEngine embeddingEngine,
            IClassificationEngine classificationEngine,
            IReportRepository reportRepository,
            ICategoryRepository categoryRepository,
            IConceptResolver conceptResolver,
            IObjectTypeCompatibilityService objectTypeCompatibilityService,
            ISemanticSearchOrchestrator semanticSearchOrchestrator,
            IOptions<HybridPipelineOptions> hybridPipelineOptions,
            ILogger<AiMatchingService> logger)
        {
            _embeddingEngine = embeddingEngine;
            _classificationEngine = classificationEngine;
            _reportRepository = reportRepository;
            _categoryRepository = categoryRepository;
            _conceptResolver = conceptResolver;
            _objectTypeCompatibilityService = objectTypeCompatibilityService;
            _semanticSearchOrchestrator = semanticSearchOrchestrator;
            _hybridPipelineOptions = hybridPipelineOptions;
            _logger = logger;
        }

        /// <summary>
        /// Finds reports similar to the given free-text and/or image search.
        /// Pipeline: Search -&gt; Classification (LLM) -&gt; Embedding -&gt;
        /// Repository Search -&gt; Hybrid Scoring (local, deterministic) -&gt;
        /// ReasonSummary -&gt; MatchExplanationGenerator (local, deterministic).
        /// No AI provider ever computes or adjusts the score, and - as of
        /// this pipeline - none is ever consulted for the explanation text
        /// either; the explanation step only describes a result that has
        /// already been finalized, entirely from facts already in memory.
        /// </summary>
        /// <param name="searchText">Free-text query.</param>
        /// <param name="imageBytes">Optional image bytes for image-based search.</param>
        /// <param name="type">Optional report type filter.</param>
        /// <param name="maxResults">Maximum number of ranked results to return.</param>
        public async Task<List<RankedReportResult>> FindSimilarReportsAsync(
            string? searchText,
            byte[]? imageBytes,
            ReportType? type,
            int maxResults = 10)
        {
            // Phase 2B Part 4: the new query/retrieval/ranking pipeline is
            // wired in for real here, behind LostFound:AI:HybridPipeline:Enabled
            // (default false). Image-based search always uses the legacy
            // path below regardless of the flag - IQueryPipeline is
            // text-only by design (see ISemanticSearchOrchestrator remarks).
            // Everything below this block (classification, deterministic
            // scoring, ObjectTypeRelationship, ConfidenceCalibrator,
            // MatchExplanationGenerator, QueryProcessingCache) is completely
            // untouched by this Part.
            if (_hybridPipelineOptions.Value.Enabled && imageBytes == null && !string.IsNullOrWhiteSpace(searchText))
            {
                _logger.LogInformation("Hybrid pipeline enabled - routing to SemanticSearchOrchestrator (text-only search).");
                return await _semanticSearchOrchestrator.SearchAsync(searchText!, type, maxResults);
            }

            _logger.LogInformation("========== AI Matching Started ==========");
            _logger.LogInformation("Search Text: {Text}", searchText);
            _logger.LogInformation("Has Image: {HasImage}", imageBytes != null);

            var classification = await ClassifySearchAsync(searchText, imageBytes);

            var (queryTextEmbedding, queryImageEmbedding) =
                await GenerateQueryEmbeddingsAsync(searchText, imageBytes, classification);

            var candidates = await _reportRepository.GetSearchableReportsAsync(type);
            _logger.LogInformation(
                "Repository returned {Count} searchable report(s).",
                candidates.Count);

            var objectTypeRelationships = await BuildObjectTypeRelationshipCacheAsync(candidates, classification);

            // Phase 3 Part 4 - see ScoringWeights.ImageOnlyTextWeight remarks.
            // Deliberately keyed off the raw query shape (image bytes present,
            // no meaningful typed text), not off whether queryImageEmbedding
            // ended up non-null - if image embedding generation itself failed
            // (Phase 3 Part 1's graceful degradation), ImageScore contributes
            // 0 under either weight profile, so the choice of profile is moot
            // in that case; this flag only needs to be right when there IS a
            // real image signal to weight.
            var isImageOnlyQuery = imageBytes != null && !HasMeaningfulText(searchText);
            if (isImageOnlyQuery)
            {
                _logger.LogInformation("Image-only query shape detected - using ImageOnly scoring weight profile (Text={TextW}, Image={ImageW}).",
                    ScoringWeights.ImageOnlyTextWeight, ScoringWeights.ImageOnlyImageWeight);
            }

            var scoredCandidates = ScoreCandidates(
                candidates, searchText, queryTextEmbedding, queryImageEmbedding, classification, objectTypeRelationships, isImageOnlyQuery);

            var topCandidates = scoredCandidates
                .OrderByDescending(sc => sc.Score.Total)
                .Take(maxResults)
                .ToList();

            _logger.LogInformation(
                "Ranked {ScoredCount} candidate(s) with a positive score, keeping top {Kept}.",
                scoredCandidates.Count,
                topCandidates.Count);

            var results = BuildRankedResults(topCandidates, searchText, classification);

            _logger.LogInformation("Final Results Count = {Count}", results.Count);
            _logger.LogInformation("========== AI Matching Finished ==========");

            return results;
        }

        // =====================================================================
        // ---- Classification & embeddings --------------------------------------
        // =====================================================================

        /// <summary>
        /// Classifies the raw search text/image via <see cref="IItemClassificationProvider"/>
        /// and stores the result in a local, non-persisted <see cref="SearchClassification"/>.
        /// Nothing here touches the database or creates a <see cref="Report"/>.
        /// </summary>
        private async Task<SearchClassification> ClassifySearchAsync(string? searchText, byte[]? imageBytes)
        {
            if (string.IsNullOrWhiteSpace(searchText) && imageBytes == null)
            {
                _logger.LogInformation("Nothing to classify (no text or image supplied).");
                return SearchClassification.Empty;
            }

            ItemClassificationResult? result;
            try
            {
                using var cts = new CancellationTokenSource(AiCallTimeout);
                result = await _classificationEngine.ClassifyAsync(searchText, imageBytes, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Classification timed out after {Timeout}s; continuing with raw search text only (no structured attributes, no AI SearchText).",
                    AiCallTimeout.TotalSeconds);
                return SearchClassification.Empty;
            }
            catch (Exception ex)
            {
                // Classification is an enhancement (structured attributes +
                // an embedding-optimized SearchText), not a hard requirement -
                // a rate limit, timeout, or any other provider failure here
                // must not take the whole search down. Degrade to an "empty"
                // classification; GenerateQueryEmbeddingsAsync already falls
                // back to embedding the raw search text in that case, and
                // structured attribute scoring simply contributes 0 instead
                // of throwing.
                _logger.LogWarning(
                    ex,
                    "Classification failed; continuing with raw search text only (no structured attributes, no AI SearchText).");
                return SearchClassification.Empty;
            }

            var classification = new SearchClassification(
                result?.CategoryName,
                result?.ObjectType,
                result?.Color,
                result?.Brand,
                result?.Tags ?? new List<string>(),
                result?.SearchText);

            _logger.LogInformation(
                "Classification -> Category: {Category}, ObjectType: {ObjectType}, Color: {Color}, Brand: {Brand}, Tags: {Tags}, SearchText: {SearchText}",
                classification.CategoryName,
                classification.ObjectType,
                classification.Color,
                classification.Brand,
                string.Join(", ", classification.Tags),
                classification.SearchText);

            return classification;
        }

        /// <summary>
        /// Generates the text and image embeddings for the search query exactly
        /// once, reused for every candidate. The text embedding is generated
        /// from <see cref="SearchClassification.SearchText"/> - the natural-
        /// language, embedding-optimized paragraph produced by
        /// <see cref="IItemClassificationProvider"/> - since that is the field
        /// specifically engineered for semantic search quality. If the
        /// provider didn't return one (e.g. classification failed or was
        /// skipped), falls back to a simple concatenation of the raw search
        /// text and classification attributes so search still works.
        /// </summary>
        private async Task<(float[]? TextEmbedding, float[]? ImageEmbedding)> GenerateQueryEmbeddingsAsync(
            string? searchText,
            byte[]? imageBytes,
            SearchClassification classification)
        {
            var textEmbedding = await GenerateQueryTextEmbeddingAsync(searchText, classification);

            float[]? imageEmbedding = null;
            if (imageBytes != null)
            {
                // Mirrors the existing try/catch already used for the same
                // call in ReportMatchingBackgroundJob (see that class's
                // remarks) - image embedding has no local/offline path (it's
                // always an external-provider "caption-then-embed" call), so
                // a provider auth failure, timeout, rate limit, or network
                // error here is expected, recoverable operating behavior,
                // not a reason to fail the whole search. Before this fix, an
                // unhandled exception here propagated all the way up through
                // AiSearchAppService.SearchAsync as a raw provider error
                // (e.g. 401) instead of degrading to text-only scoring -
                // confirmed live in Luqya-System-Reference.md §20/§32.
                try
                {
                    imageEmbedding = await _embeddingEngine.GenerateImageEmbeddingAsync(imageBytes);
                    _logger.LogInformation("Query Image Embedding Length = {Length}", imageEmbedding.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Image search degraded to text-only: query image embedding generation failed; " +
                        "continuing with text-only scoring for this search request.");
                    imageEmbedding = null;
                }
            }

            return (textEmbedding, imageEmbedding);
        }

        /// <summary>
        /// Builds the query text embedding. The raw embedding input (AI
        /// <c>SearchText</c>, or the fallback concatenation) is first run
        /// through <see cref="SearchTextProcessor"/> - normalization, intent-
        /// word/filler removal, synonym expansion (Tasks 1-3) - which is what
        /// lets differently-worded-but-semantically-identical queries
        /// converge on the same embedding. Both the normalized text and the
        /// resulting embedding are cached by <see cref="QueryProcessingCache"/>
        /// (Task 9) so a repeated search never re-normalizes or re-calls the
        /// embedding API for text it has already processed.
        /// </summary>
        private async Task<float[]?> GenerateQueryTextEmbeddingAsync(string? searchText, SearchClassification classification)
        {
            var rawInput = !string.IsNullOrWhiteSpace(classification.SearchText)
                ? classification.SearchText!
                : BuildEnrichedQueryText(searchText, classification);

            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return null;
            }

            var normalized = QueryProcessingCache.GetOrAddNormalizedText(rawInput, SearchTextProcessor.Process);

            // A query that was ONLY intent/filler words (rare, but possible)
            // normalizes down to nothing - fall back to the raw input rather
            // than embedding an empty string.
            var semanticText = string.IsNullOrWhiteSpace(normalized) ? rawInput : normalized;

            _logger.LogInformation(
                "Query Normalization -> Raw: \"{Raw}\" | Normalized/Expanded: \"{Normalized}\" (source: {Source})",
                rawInput,
                semanticText,
                !string.IsNullOrWhiteSpace(classification.SearchText) ? "AI SearchText" : "fallback concatenation");

            if (QueryProcessingCache.TryGetEmbedding(semanticText, out var cached))
            {
                _logger.LogInformation("Query Text Embedding Length = {Length} (source: cache hit)", cached!.Length);
                return cached;
            }

            var embedding = await _embeddingEngine.GenerateEmbeddingAsync(semanticText);
            QueryProcessingCache.SetEmbedding(semanticText, embedding);

            _logger.LogInformation("Query Text Embedding Length = {Length} (source: embedding API)", embedding.Length);

            return embedding;
        }

        /// <summary>
        /// Fallback embedding input used only when the classification provider
        /// did not return a <see cref="SearchClassification.SearchText"/> (e.g.
        /// classification failed, was skipped, or the provider is a stub).
        /// Simple concatenation is intentionally a lower-quality fallback, not
        /// the primary path - see <see cref="GenerateQueryEmbeddingsAsync"/>.
        /// </summary>
        private static string BuildEnrichedQueryText(string? searchText, SearchClassification classification)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                parts.Add(searchText!);
            }

            if (!string.IsNullOrWhiteSpace(classification.ObjectType))
            {
                parts.Add(classification.ObjectType!);
            }

            if (!string.IsNullOrWhiteSpace(classification.Color))
            {
                parts.Add(classification.Color!);
            }

            if (!string.IsNullOrWhiteSpace(classification.Brand))
            {
                parts.Add(classification.Brand!);
            }

            if (!string.IsNullOrWhiteSpace(classification.CategoryName))
            {
                parts.Add(classification.CategoryName!);
            }

            parts.AddRange(classification.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));

            return string.Join(". ", parts);
        }

        // =====================================================================
        // ---- Deterministic scoring (never touched by any AI provider) --------
        // =====================================================================

        /// <summary>
        /// Scores every candidate report against the query embeddings and
        /// classification, logging each stage, and returns only the
        /// candidates with a positive combined score. Purely local math - no
        /// network call, no LLM.
        /// </summary>
        /// <summary>
        /// Phase 3 Part 4 - precise, consistent definition of "the query has
        /// real typed text" used to decide whether the image-only scoring
        /// weight profile applies (see <see cref="ScoringWeights.ImageOnlyTextWeight"/>).
        /// Null/empty/whitespace-only text is obviously not meaningful; a
        /// single stray character (e.g. an accidental space+letter left in
        /// the box) is deliberately also excluded so it can't flip a
        /// genuinely image-driven search into the text-dominant weight
        /// profile - two non-whitespace characters is the minimum required.
        /// </summary>
        private static bool HasMeaningfulText(string? searchText) =>
            !string.IsNullOrWhiteSpace(searchText) && searchText.Trim().Length >= 2;

        private List<ScoredCandidate> ScoreCandidates(
            List<Report> candidates,
            string? searchText,
            float[]? queryTextEmbedding,
            float[]? queryImageEmbedding,
            SearchClassification classification,
            IReadOnlyDictionary<(string ObjectType, Guid? CategoryId), ObjectTypeRelationship.Relationship> objectTypeRelationships,
            bool isImageOnlyQuery)
        {
            var scored = new List<ScoredCandidate>();

            foreach (var candidate in candidates)
            {
                _logger.LogInformation("------------------------------------");
                _logger.LogInformation("Report: {Id}", candidate.Id);
                _logger.LogInformation("Description: {Description}", candidate.Description);

                if (IsClearlyIncompatible(candidate, classification, objectTypeRelationships))
                {
                    _logger.LogInformation(
                        "Early-filtered: object type '{CandidateType}' is unrelated to query object type '{QueryType}' - skipped before scoring.",
                        candidate.AiObjectType,
                        classification.ObjectType);
                    continue;
                }

                var commonTags = candidate.HasTags()
                    ? candidate.GetNormalizedTags().Intersect(classification.NormalizedTags).ToList()
                    : new List<string>();

                var score = BuildCandidateScore(candidate, searchText, queryTextEmbedding, queryImageEmbedding, classification, commonTags.Count, objectTypeRelationships, isImageOnlyQuery);

                _logger.LogInformation(
                    "Text={Text} Image={Image} ObjectType={ObjectType} Color={Color} Brand={Brand} Tag={Tag} Location={Location} Date={Date} DynamicBoost={DynamicBoost} Penalty={Penalty}",
                    score.TextScore, score.ImageScore, score.ObjectTypeScore, score.ColorScore,
                    score.BrandScore, score.TagScore, score.LocationScore, score.DateScore, score.DynamicBoost, score.Penalty);
                _logger.LogInformation("Combined Score (raw) = {Score}", score.Total);

                if (score.Total <= 0)
                {
                    _logger.LogInformation("Skipped.");
                    continue;
                }

                scored.Add(new ScoredCandidate(candidate, score, commonTags));
            }

            return scored;
        }

        /// <summary>
        /// Task 8 - cheap pre-check that discards candidates whose object
        /// type is definitively unrelated to the search's before running any
        /// embedding math on them. Reads from the same precomputed
        /// <paramref name="objectTypeRelationships"/> cascade result the
        /// tiered penalty in <see cref="CalculatePenalty"/> reads from (see
        /// <see cref="BuildObjectTypeRelationshipCacheAsync"/>), so "discarded
        /// here" and "heavily penalized there" never disagree - this is
        /// purely a CPU shortcut, not a separate rule. Only ever returns
        /// <c>true</c> for the <see cref="ObjectTypeRelationship.Relationship.UnrelatedCluster"/>
        /// tier; anything less certain still goes through full scoring so a
        /// genuine match is never silently dropped.
        /// </summary>
        private static bool IsClearlyIncompatible(
            Report candidate,
            SearchClassification classification,
            IReadOnlyDictionary<(string ObjectType, Guid? CategoryId), ObjectTypeRelationship.Relationship> objectTypeRelationships)
        {
            if (!candidate.HasObjectType() || string.IsNullOrWhiteSpace(classification.ObjectType))
            {
                return false;
            }

            var candidateType = candidate.GetNormalizedObjectType();
            var queryType = classification.NormalizedObjectType;

            if (candidateType == queryType)
            {
                return false;
            }

            return objectTypeRelationships.TryGetValue((candidateType, candidate.CategoryId), out var relationship)
                   && relationship == ObjectTypeRelationship.Relationship.UnrelatedCluster;
        }

        /// <summary>
        /// Builds the per-search cache <see cref="IsClearlyIncompatible"/> and
        /// <see cref="CalculatePenalty"/> both read from - one entry per
        /// distinct (candidate object type, candidate category) pair actually
        /// present in <paramref name="candidates"/>, so an ontology/DB call is
        /// never repeated for two candidates that happen to share the same
        /// object type and category (many "Wallet"/"Bags"-category reports in
        /// a typical result set, for example).
        /// <para>
        /// Implements tiers 2-4 of the generalized object-type-equivalence
        /// cascade validated in
        /// <c>Generalized-Concept-Equivalence-Cascade-Architecture-and-Validation-2026-08-07.md</c>
        /// (tier 1, exact match, is handled entirely by the two callers
        /// before they ever consult this cache - a query/candidate pair with
        /// identical normalized object types never reaches tier 2). Two
        /// values are resolved ONCE per search, not per candidate: the
        /// query's own <see cref="Report.CategoryId"/>-equivalent (a
        /// read-only lookup via <see cref="ICategoryRepository.FindByNameAsync"/>
        /// - deliberately not <c>CategoryManager.FindOrCreateByNameAsync</c>,
        /// since a search must never create a new <c>Category</c> row as a
        /// side effect), and the query's resolved ontology concept (via
        /// <see cref="IConceptResolver"/>). Both are then reused for every
        /// distinct candidate pair below.
        /// </para>
        /// </summary>
        private async Task<Dictionary<(string ObjectType, Guid? CategoryId), ObjectTypeRelationship.Relationship>> BuildObjectTypeRelationshipCacheAsync(
            List<Report> candidates,
            SearchClassification classification)
        {
            var cache = new Dictionary<(string ObjectType, Guid? CategoryId), ObjectTypeRelationship.Relationship>();

            if (string.IsNullOrWhiteSpace(classification.ObjectType))
            {
                return cache;
            }

            var queryType = classification.NormalizedObjectType;

            Guid? queryCategoryId = null;
            if (!string.IsNullOrWhiteSpace(classification.CategoryName))
            {
                var queryCategory = await _categoryRepository.FindByNameAsync(classification.CategoryName!);
                queryCategoryId = queryCategory?.Id;
            }

            // Classification output (both the query's and every candidate's)
            // is reliably English regardless of the source text's own
            // language - see ConceptResolver's own remarks on this - so "en"
            // is passed consistently everywhere the ontology is consulted
            // below, matching how every other ranking-relevant consumer of
            // IConceptResolver in this codebase already resolves this field.
            var queryConcept = await _conceptResolver.ResolveAsync(classification.ObjectType!, "en");

            var distinctCandidatePairs = candidates
                .Where(c => c.HasObjectType())
                .Select(c => new { Normalized = c.GetNormalizedObjectType(), Raw = c.AiObjectType!, c.CategoryId })
                .Where(x => x.Normalized != queryType)
                .GroupBy(x => (x.Normalized, x.CategoryId))
                .Select(g => g.First());

            var tier4Hits = 0;

            foreach (var pair in distinctCandidatePairs)
            {
                var relationship = await ResolveObjectTypeRelationshipAsync(
                    pair.Normalized, pair.Raw, queryType, pair.CategoryId, queryCategoryId, queryConcept?.Id);

                if (relationship.Tier == ObjectTypeRelationshipTier.Ontology)
                {
                    tier4Hits++;
                }

                cache[(pair.Normalized, pair.CategoryId)] = relationship.Relationship;
            }

            if (tier4Hits > 0)
            {
                _logger.LogInformation(
                    "Object-type cascade: {Tier4Hits} of {Distinct} distinct candidate object-type/category pair(s) reached the ontology fallback tier (tier 4).",
                    tier4Hits, cache.Count);
            }

            return cache;
        }

        /// <summary>
        /// Resolves one candidate object-type/category pair through tiers
        /// 2-4 of the cascade, cheapest and safest first, stopping at the
        /// first tier that produces a definitive answer:
        /// <list type="number">
        /// <item>Category-equality gate: the candidate's own <see cref="Report.CategoryId"/>
        /// equals the query's resolved category. Only ever reached because
        /// the caller already ruled out an exact object-type match, so this
        /// tier's own known noise (two reports sharing an object type but
        /// disagreeing on category) never has a chance to override an
        /// already-correct exact match. Mapped to <see cref="ObjectTypeRelationship.Relationship.RelatedCluster"/>
        /// (today's moderate penalty), not a full bonus/no-penalty treatment
        /// - deliberately conservative, since category agreement is a
        /// weaker signal than an actual identical object type.</item>
        /// <item>The existing static <see cref="ObjectTypeRelationship"/>
        /// cluster table - completely unchanged, including the Phone/Smartphone-style
        /// clustering it already encodes.</item>
        /// <item>The ontology fallback (<see cref="IConceptResolver"/> +
        /// <see cref="IObjectTypeCompatibilityService"/>) - unchanged
        /// implementation, unchanged guardrails
        /// (<c>MinTokenCountForSemanticFallback</c>, <c>MinSemanticSimilarity</c>),
        /// only a new caller. An ontology <see cref="ObjectTypeCompatibility.Same"/>
        /// verdict (two different surface strings resolving to the identical
        /// curated concept, e.g. "Wallet"/"Purse") is deliberately mapped to
        /// <see cref="ObjectTypeRelationship.Relationship.RelatedCluster"/>
        /// here too, not full Same/no-penalty - this cascade only ever
        /// softens the PENALTY tier for a non-identical string pair, it never
        /// grants <see cref="ScoringWeights.ObjectTypeBonus"/>, which stays
        /// exact-match-only (see <see cref="CalculateObjectTypeScore"/>,
        /// untouched). <see cref="ObjectTypeCompatibility.RelatedCluster"/>
        /// (e.g. the Phone/Smartphone parent-child pair) is passed through as
        /// <see cref="ObjectTypeRelationship.Relationship.RelatedCluster"/>
        /// unchanged - explicitly NOT promoted to Same, preserving the exact
        /// tiering that a prior, unrelated change already proved regresses
        /// aggregate ranking quality when merged.</item>
        /// </list>
        /// Falls through to <see cref="ObjectTypeRelationship.Relationship.Unknown"/>
        /// (today's existing default) when none of the three tiers resolves
        /// anything - identical to this pair's treatment before this cascade
        /// existed.
        /// </summary>
        private async Task<(ObjectTypeRelationship.Relationship Relationship, ObjectTypeRelationshipTier Tier)> ResolveObjectTypeRelationshipAsync(
            string candidateTypeNormalized,
            string candidateTypeRaw,
            string queryTypeNormalized,
            Guid? candidateCategoryId,
            Guid? queryCategoryId,
            Guid? queryConceptId)
        {
            // Tier 2: Category-equality gate.
            if (candidateCategoryId.HasValue && queryCategoryId.HasValue && candidateCategoryId.Value == queryCategoryId.Value)
            {
                return (ObjectTypeRelationship.Relationship.RelatedCluster, ObjectTypeRelationshipTier.Category);
            }

            // Tier 3: existing static high-confidence clusters - unchanged.
            var clusterRelationship = ObjectTypeRelationship.Classify(candidateTypeNormalized, queryTypeNormalized);
            if (clusterRelationship != ObjectTypeRelationship.Relationship.Unknown)
            {
                return (clusterRelationship, ObjectTypeRelationshipTier.StaticCluster);
            }

            // Tier 4: ontology fallback - only reached once tiers 2-3 both
            // failed to produce a definitive answer.
            if (queryConceptId.HasValue)
            {
                var compatibility = await _objectTypeCompatibilityService.ClassifyAsync(queryConceptId, candidateTypeRaw, "en");

                var mapped = compatibility switch
                {
                    ObjectTypeCompatibility.Same => ObjectTypeRelationship.Relationship.RelatedCluster,
                    ObjectTypeCompatibility.RelatedCluster => ObjectTypeRelationship.Relationship.RelatedCluster,
                    ObjectTypeCompatibility.UnrelatedCluster => ObjectTypeRelationship.Relationship.UnrelatedCluster,
                    _ => ObjectTypeRelationship.Relationship.Unknown
                };

                if (mapped != ObjectTypeRelationship.Relationship.Unknown)
                {
                    return (mapped, ObjectTypeRelationshipTier.Ontology);
                }
            }

            // Tier 5: no signal resolved this pair - today's existing default.
            return (ObjectTypeRelationship.Relationship.Unknown, ObjectTypeRelationshipTier.None);
        }

        /// <summary>
        /// Which cascade tier produced a given <see cref="ObjectTypeRelationship.Relationship"/>
        /// - diagnostic/latency-measurement only, never read by scoring
        /// itself (<see cref="IsClearlyIncompatible"/>/<see cref="CalculatePenalty"/>
        /// only ever read the <c>Relationship</c> half of the tuple).
        /// </summary>
        private enum ObjectTypeRelationshipTier
        {
            None,
            Category,
            StaticCluster,
            Ontology
        }

        /// <summary>
        /// Computes every individual score component plus the penalty for a
        /// single candidate report and packages them into a
        /// <see cref="CandidateScore"/>. This is the single source of truth
        /// for the final score - the explanation layer only ever reads from it.
        /// </summary>
        private static CandidateScore BuildCandidateScore(
            Report candidate,
            string? searchText,
            float[]? queryTextEmbedding,
            float[]? queryImageEmbedding,
            SearchClassification classification,
            int commonTagCount,
            IReadOnlyDictionary<(string ObjectType, Guid? CategoryId), ObjectTypeRelationship.Relationship> objectTypeRelationships,
            bool isImageOnlyQuery)
        {
            return new CandidateScore
            {
                TextScore = CalculateTextScore(queryTextEmbedding, candidate.GetEmbeddingVector()),
                ImageScore = CalculateImageScore(queryImageEmbedding, candidate.GetImageEmbeddingVector()),
                ObjectTypeScore = CalculateObjectTypeScore(candidate, classification),
                ColorScore = CalculateColorScore(candidate, classification),
                BrandScore = CalculateBrandScore(candidate, classification),
                TagScore = CalculateTagScore(candidate, classification, commonTagCount),
                LocationScore = CalculateLocationScore(candidate, classification),
                DateScore = CalculateDateScore(candidate, classification),
                DynamicBoost = CalculateDynamicBoosts(candidate, searchText, commonTagCount),
                Penalty = CalculatePenalty(candidate, classification, objectTypeRelationships),
                IsImageOnlyQuery = isImageOnlyQuery
            };
        }

        /// <summary>
        /// Semantic text similarity between the query and candidate
        /// embeddings, expressed as a 0-100 percentage. Returns 0 when either
        /// embedding is unavailable.
        /// </summary>
        private static double CalculateTextScore(float[]? queryEmbedding, float[]? candidateEmbedding)
        {
            if (queryEmbedding == null || candidateEmbedding == null || candidateEmbedding.Length == 0)
            {
                return 0;
            }

            return CosineSimilarityCalculator.CalculatePercentage(queryEmbedding, candidateEmbedding);
        }

        /// <summary>
        /// Image similarity between the query and candidate embeddings,
        /// expressed as a 0-100 percentage. Returns 0 when either embedding is
        /// unavailable.
        /// </summary>
        private static double CalculateImageScore(float[]? queryEmbedding, float[]? candidateEmbedding)
        {
            if (queryEmbedding == null || candidateEmbedding == null || candidateEmbedding.Length == 0)
            {
                return 0;
            }

            return CosineSimilarityCalculator.CalculatePercentage(queryEmbedding, candidateEmbedding);
        }

        /// <summary>
        /// +<see cref="ScoringWeights.ObjectTypeBonus"/> when the candidate's
        /// AI-resolved object type matches the search classification's object
        /// type, otherwise 0. Uses <see cref="Report.HasObjectType"/> and
        /// <see cref="Report.GetNormalizedObjectType"/> rather than comparing
        /// raw strings.
        /// </summary>
        private static double CalculateObjectTypeScore(Report candidate, SearchClassification classification)
        {
            if (!candidate.HasObjectType() || string.IsNullOrWhiteSpace(classification.ObjectType))
            {
                return 0;
            }

            return candidate.GetNormalizedObjectType() == classification.NormalizedObjectType
                ? ScoringWeights.ObjectTypeBonus
                : 0;
        }

        /// <summary>
        /// +<see cref="ScoringWeights.ColorBonus"/> when the candidate's
        /// AI-resolved color matches the search classification's color,
        /// otherwise 0.
        /// </summary>
        private static double CalculateColorScore(Report candidate, SearchClassification classification)
        {
            if (!candidate.HasColor() || string.IsNullOrWhiteSpace(classification.Color))
            {
                return 0;
            }

            return candidate.GetNormalizedColor() == classification.NormalizedColor
                ? ScoringWeights.ColorBonus
                : 0;
        }

        /// <summary>
        /// +<see cref="ScoringWeights.BrandBonus"/> when the candidate's
        /// AI-resolved brand matches the search classification's brand,
        /// otherwise 0.
        /// </summary>
        private static double CalculateBrandScore(Report candidate, SearchClassification classification)
        {
            if (!candidate.HasBrand() || string.IsNullOrWhiteSpace(classification.Brand))
            {
                return 0;
            }

            return candidate.GetNormalizedBrand() == classification.NormalizedBrand
                ? ScoringWeights.BrandBonus
                : 0;
        }

        /// <summary>
        /// Up to <see cref="ScoringWeights.TagBonus"/> points, scaled by the
        /// proportion of tags the candidate and the search classification
        /// have in common (common tags / largest tag set). Returns 0 when
        /// either side has no tags.
        /// </summary>
        private static double CalculateTagScore(Report candidate, SearchClassification classification, int commonTagCount)
        {
            if (!candidate.HasTags() || classification.NormalizedTags.Count == 0)
            {
                return 0;
            }

            var largestSetSize = Math.Max(candidate.GetNormalizedTags().Count, classification.NormalizedTags.Count);
            if (largestSetSize == 0)
            {
                return 0;
            }

            return ScoringWeights.TagBonus * ((double)commonTagCount / largestSetSize);
        }

        /// <summary>
        /// Extension point for a future location-proximity boost. Intentionally
        /// not implemented yet - always returns <see cref="ScoringWeights.LocationBonus"/> (0).
        /// </summary>
        private static double CalculateLocationScore(Report candidate, SearchClassification classification)
        {
            return ScoringWeights.LocationBonus;
        }

        /// <summary>
        /// Extension point for a future date-proximity boost. Intentionally
        /// not implemented yet - always returns <see cref="ScoringWeights.DateBonus"/> (0).
        /// </summary>
        private static double CalculateDateScore(Report candidate, SearchClassification classification)
        {
            return ScoringWeights.DateBonus;
        }

        /// <summary>
        /// Dedicated, isolated penalty calculation, kept separate from the
        /// bonus methods so new "impossible combination" rules can be added
        /// in one place. Task 6: a flat penalty isn't enough - Phone-vs-Wallet
        /// and Phone-vs-Car are both "wrong", but not equally wrong. When both
        /// sides have an object type and they disagree, the penalty tier comes
        /// from the precomputed <paramref name="objectTypeRelationships"/>
        /// cascade result (see <see cref="BuildObjectTypeRelationshipCacheAsync"/>
        /// and <see cref="ResolveObjectTypeRelationshipAsync"/> for tiers
        /// 2-4 - Category-equality gate, the static <see cref="ObjectTypeRelationship"/>
        /// clusters, then the ontology fallback): a pair the cascade never
        /// resolved falls back to the original flat penalty, a pair resolved
        /// as related (same category, same static cluster, or ontology
        /// RelatedCluster/Same) gets a moderate penalty, and a pair resolved
        /// as definitively unrelated gets a much larger one - see
        /// <see cref="ScoringWeights"/>.
        /// </summary>
        private static double CalculatePenalty(
            Report candidate,
            SearchClassification classification,
            IReadOnlyDictionary<(string ObjectType, Guid? CategoryId), ObjectTypeRelationship.Relationship> objectTypeRelationships)
        {
            if (!candidate.HasObjectType() || string.IsNullOrWhiteSpace(classification.ObjectType))
            {
                return 0;
            }

            var candidateType = candidate.GetNormalizedObjectType();
            var queryType = classification.NormalizedObjectType;

            if (candidateType == queryType)
            {
                return 0;
            }

            var relationship = objectTypeRelationships.TryGetValue((candidateType, candidate.CategoryId), out var resolved)
                ? resolved
                : ObjectTypeRelationship.Relationship.Unknown;

            return relationship switch
            {
                ObjectTypeRelationship.Relationship.RelatedCluster => ScoringWeights.RelatedClusterPenalty,
                ObjectTypeRelationship.Relationship.UnrelatedCluster => ScoringWeights.UnrelatedClusterPenalty,
                _ => ScoringWeights.UnknownObjectTypePenalty
            };

            // Extension point: additional "impossible combination" rules
            // (e.g. conflicting statuses) can be added here without touching
            // the bonus methods above.
        }

        /// <summary>
        /// Task 5 - boosts a flat weight table can't express on its own:
        /// <list type="bullet">
        /// <item>Many shared tags: scales up to <see cref="ScoringWeights.MaxSharedTagsBoost"/>
        /// as the common-tag count grows, on top of the proportional base
        /// <see cref="CalculateTagScore"/> already gives - two candidates
        /// with the same tag PROPORTION don't necessarily deserve the same
        /// boost if one shares 2 tags and the other shares 8.</item>
        /// <item>Exact product-model mention: the raw search text contains a
        /// distinctive word (4+ alphanumeric characters) that also appears
        /// verbatim in the candidate's description - a strong signal no
        /// embedding-similarity score fully captures (model numbers, unusual
        /// spellings, etc).</item>
        /// <item>Exact color/brand mention: the raw search text literally
        /// contains the candidate's own color/brand word, beyond just having
        /// matched after normalization.</item>
        /// </list>
        /// Capped at <see cref="ScoringWeights.MaxDynamicBoost"/> by construction
        /// (each component is independently capped, and they're summed).
        /// </summary>
        private static double CalculateDynamicBoosts(Report candidate, string? searchText, int commonTagCount)
        {
            double boost = 0;

            if (commonTagCount > 0)
            {
                // Reaches the max at 5+ shared tags; scales linearly below that.
                boost += Math.Min(commonTagCount * 2.0, ScoringWeights.MaxSharedTagsBoost);
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return boost;
            }

            var loweredSearchText = searchText.ToLowerInvariant();

            if (ContainsExactModelMention(loweredSearchText, candidate.Description))
            {
                boost += ScoringWeights.ExactModelMentionBoost;
            }

            if (!string.IsNullOrWhiteSpace(candidate.Color) &&
                loweredSearchText.Contains(candidate.Color!.ToLowerInvariant()))
            {
                boost += ScoringWeights.ExactColorMentionBoost;
            }

            if (!string.IsNullOrWhiteSpace(candidate.AiBrand) &&
                loweredSearchText.Contains(candidate.AiBrand!.ToLowerInvariant()))
            {
                boost += ScoringWeights.ExactBrandMentionBoost;
            }

            return boost;
        }

        /// <summary>
        /// True if the search text and the candidate's description share a
        /// distinctive word (length &gt;= 4, so short common words like
        /// "the"/"من" can't trigger it by accident) - a cheap stand-in for
        /// "the user typed the exact model name/number" without needing a
        /// dedicated model-name database.
        /// </summary>
        private static bool ContainsExactModelMention(string loweredSearchText, string? candidateDescription)
        {
            if (string.IsNullOrWhiteSpace(candidateDescription))
            {
                return false;
            }

            var loweredDescription = candidateDescription.ToLowerInvariant();

            foreach (var token in loweredSearchText.Split(
                         (char[]?)null,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length >= 4 && loweredDescription.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        // =====================================================================
        // ---- Explanation layer (local, deterministic, no AI call) ------------
        //
        // Everything below this point only DESCRIBES a score that has already
        // been finalized above. It never recalculates, adjusts, or influences
        // ScorePercentage. There used to be an optional LLM call here
        // (IMatchExplanationProvider) - it has been removed entirely in favor
        // of MatchExplanationGenerator, so this layer can no longer fail,
        // time out, or add latency: it's plain in-memory string building.
        // =====================================================================

        /// <summary>
        /// Builds the final <see cref="RankedReportResult"/> list for the
        /// already-ranked top candidates, attaching both a structured
        /// <c>MatchReasons</c> list and a natural-language, multilingual
        /// <c>MatchExplanation</c> paragraph to each one. Purely local work
        /// (no I/O, no AI call), so it's a plain synchronous loop - no
        /// <c>Task.WhenAll</c>/<c>async</c> machinery is needed anymore now
        /// that there is nothing to run concurrently.
        /// </summary>
        private List<RankedReportResult> BuildRankedResults(
            List<ScoredCandidate> topCandidates,
            string? searchText,
            SearchClassification classification)
        {
            var results = new List<RankedReportResult>(topCandidates.Count);

            foreach (var scored in topCandidates)
            {
                results.Add(BuildSingleRankedResult(scored, searchText, classification));
            }

            return results;
        }

        /// <summary>
        /// Builds one candidate's <see cref="RankedReportResult"/>, including
        /// its natural-language explanation from <see cref="MatchExplanationGenerator"/>.
        /// </summary>
        private RankedReportResult BuildSingleRankedResult(
            ScoredCandidate scored,
            string? searchText,
            SearchClassification classification)
        {
            var reasonSummary = BuildReasonSummary(scored, classification, searchText);
            var explanation = MatchExplanationGenerator.Build(reasonSummary);

            _logger.LogInformation(
                "Report {Id} explanation: {Explanation}",
                scored.Candidate.Id,
                explanation);

            return new RankedReportResult
            {
                ReportId = scored.Candidate.Id,
                Description = scored.Candidate.Description,
                Color = scored.Candidate.Color,
                AiObjectType = scored.Candidate.AiObjectType,
                Type = scored.Candidate.Type,
                ImagePath = scored.Candidate.ImagePath,
                ScorePercentage = reasonSummary.Scoring.FinalScore,
                MatchReasons = BuildStructuredReasonList(reasonSummary),
                MatchExplanation = explanation
            };
        }

        /// <summary>
        /// Converts a scored candidate, the search classification, and the
        /// raw search text into the structured <see cref="ReasonSummary"/>
        /// that <see cref="MatchExplanationGenerator"/> and
        /// <see cref="BuildStructuredReasonList"/> are both built from. This
        /// is the only place score data is translated into "explainable"
        /// facts - nothing here recalculates the raw score, but
        /// <see cref="ScoringSideInfo.FinalScore"/> IS the Task 7 calibrated
        /// confidence (see <see cref="ConfidenceCalibrator"/>), not the raw
        /// <c>CandidateScore.Total</c> - ranking already happened on the raw
        /// value before this method runs, so calibrating here only affects
        /// what the user sees, never which results were chosen or their order.
        /// </summary>
        private static ReasonSummary BuildReasonSummary(
            ScoredCandidate scored,
            SearchClassification classification,
            string? searchText)
        {
            var candidate = scored.Candidate;
            var score = scored.Score;
            var calibratedScore = ConfidenceCalibrator.Calibrate(score.Total);

            var search = new SearchSideInfo(
                SearchText: searchText,
                ObjectType: classification.ObjectType,
                Color: classification.Color,
                Brand: classification.Brand,
                Tags: classification.Tags);

            var candidateInfo = new CandidateSideInfo(
                Description: candidate.Description,
                ObjectType: candidate.AiObjectType,
                Color: candidate.Color,
                Brand: candidate.AiBrand,
                Tags: candidate.GetAiTags());

            var scoring = new ScoringSideInfo(
                TextSimilarity: Math.Round(score.TextScore, 1),
                ImageSimilarity: Math.Round(score.ImageScore, 1),
                ObjectTypeMatched: score.ObjectTypeScore > 0,
                ColorMatched: score.ColorScore > 0,
                BrandMatched: score.BrandScore > 0,
                CommonTags: scored.CommonTags,
                Penalty: score.Penalty,
                FinalScore: Math.Round(calibratedScore, 1));

            return new ReasonSummary(search, candidateInfo, scoring);
        }

        /// <summary>
        /// Builds the structured, technical list of matching facts returned
        /// as <c>RankedReportResult.MatchReasons</c> - short labels intended
        /// for UI chips/badges, distinct from the natural-language
        /// <c>MatchExplanation</c> paragraph.
        /// </summary>
        private static List<string> BuildStructuredReasonList(ReasonSummary summary)
        {
            var scoring = summary.Scoring;
            var candidate = summary.Candidate;
            var reasons = new List<string>();

            if (scoring.TextSimilarity >= ReasonThresholds.High)
            {
                reasons.Add("high text similarity");
            }
            else if (scoring.TextSimilarity >= ReasonThresholds.Moderate)
            {
                reasons.Add("moderate text similarity");
            }

            if (scoring.ImageSimilarity >= ReasonThresholds.High)
            {
                reasons.Add("high image similarity");
            }
            else if (scoring.ImageSimilarity >= ReasonThresholds.Moderate)
            {
                reasons.Add("some image similarity");
            }

            if (scoring.ObjectTypeMatched)
            {
                reasons.Add(!string.IsNullOrWhiteSpace(candidate.ObjectType)
                    ? $"same object type ({candidate.ObjectType})"
                    : "same object type");
            }

            if (scoring.ColorMatched)
            {
                reasons.Add(!string.IsNullOrWhiteSpace(candidate.Color)
                    ? $"same color ({candidate.Color})"
                    : "same color");
            }

            if (scoring.BrandMatched)
            {
                reasons.Add(!string.IsNullOrWhiteSpace(candidate.Brand)
                    ? $"same brand ({candidate.Brand})"
                    : "same brand");
            }

            if (scoring.CommonTags.Count > 0)
            {
                reasons.Add($"{scoring.CommonTags.Count} common tag(s): {string.Join(", ", scoring.CommonTags)}");
            }

            if (scoring.Penalty < 0)
            {
                reasons.Add("object type mismatch penalty applied");
            }

            return reasons;
        }

        // =====================================================================
        // ---- Local, non-persisted value types ---------------------------------
        // =====================================================================

        /// <summary>
        /// In-memory, non-persisted result of classifying the search text/image.
        /// Deliberately not a <see cref="Report"/> - the query is never turned
        /// into a domain entity or saved. Normalization reuses
        /// <see cref="Report.NormalizeAttribute"/> / <see cref="Report.NormalizeTagList"/>
        /// so query and candidate attributes compare consistently without
        /// duplicating the normalization rule.
        /// </summary>
        private sealed class SearchClassification
        {
            public static readonly SearchClassification Empty = new(null, null, null, null, new List<string>(), null);

            public SearchClassification(
                string? categoryName,
                string? objectType,
                string? color,
                string? brand,
                List<string> tags,
                string? searchText)
            {
                CategoryName = categoryName;
                ObjectType = objectType;
                Color = color;
                Brand = brand;
                Tags = tags ?? new List<string>();
                SearchText = searchText;

                NormalizedObjectType = Report.NormalizeAttribute(objectType);
                NormalizedColor = Report.NormalizeAttribute(color);
                NormalizedBrand = Report.NormalizeAttribute(brand);
                NormalizedTags = Report.NormalizeTagList(Tags);
            }

            public string? CategoryName { get; }
            public string? ObjectType { get; }
            public string? Color { get; }
            public string? Brand { get; }
            public List<string> Tags { get; }

            /// <summary>
            /// The AI-generated, embedding-optimized natural-language paragraph
            /// from <see cref="IItemClassificationProvider"/>. Preferred over
            /// any local concatenation when generating the query embedding -
            /// see <see cref="AiMatchingService.GenerateQueryEmbeddingsAsync"/>.
            /// </summary>
            public string? SearchText { get; }

            public string NormalizedObjectType { get; }
            public string NormalizedColor { get; }
            public string NormalizedBrand { get; }
            public List<string> NormalizedTags { get; }
        }

        /// <summary>
        /// All individual score components for one candidate, plus the
        /// computed total. This is the sole source of truth for
        /// <c>ScorePercentage</c> - the explanation layer only reads from it.
        /// </summary>
        private sealed class CandidateScore
        {
            public double TextScore { get; init; }
            public double ImageScore { get; init; }
            public double ObjectTypeScore { get; init; }
            public double ColorScore { get; init; }
            public double BrandScore { get; init; }
            public double TagScore { get; init; }
            public double LocationScore { get; init; }
            public double DateScore { get; init; }

            /// <summary>Task 5 - see <see cref="AiMatchingService.CalculateDynamicBoosts"/>.</summary>
            public double DynamicBoost { get; init; }

            public double Penalty { get; init; }

            /// <summary>
            /// Phase 3 Part 4 - true when this candidate was scored against
            /// an image-only query (see <see cref="AiMatchingService.HasMeaningfulText"/>),
            /// selecting <see cref="ScoringWeights.ImageOnlyTextWeight"/>/
            /// <see cref="ScoringWeights.ImageOnlyImageWeight"/> below instead
            /// of the default pair. Every other score component is completely
            /// unaffected by this flag - only the Text/Image weighting differs.
            /// </summary>
            public bool IsImageOnlyQuery { get; init; }

            /// <summary>
            /// Raw combined score, roughly 0-100 (occasionally a little
            /// above, once dynamic boosts stack) or negative when penalties
            /// dominate. This is what candidates are RANKED by - see
            /// <see cref="ConfidenceCalibrator"/> for the separate, purely
            /// cosmetic remapping applied only to the displayed <c>ScorePercentage</c>.
            /// </summary>
            public double Total =>
                (TextScore * (IsImageOnlyQuery ? ScoringWeights.ImageOnlyTextWeight : ScoringWeights.TextWeight))
                + (ImageScore * (IsImageOnlyQuery ? ScoringWeights.ImageOnlyImageWeight : ScoringWeights.ImageWeight))
                + ObjectTypeScore
                + ColorScore
                + BrandScore
                + TagScore
                + LocationScore
                + DateScore
                + DynamicBoost
                + Penalty;
        }

        /// <summary>
        /// A candidate report paired with its computed score and the tags it
        /// shares with the search classification, kept together so the
        /// explanation layer never has to recompute anything already known.
        /// </summary>
        private sealed record ScoredCandidate(Report Candidate, CandidateScore Score, List<string> CommonTags);
    }

    // =========================================================================
    // ---- Explanation contract & structured facts (public, provider-agnostic) --
    // =========================================================================

    /// <summary>
    /// Search-side facts available to the explanation provider: the raw
    /// search text plus the AI classification derived from it. Raw (not
    /// normalized/lower-cased) so the explanation reads naturally.
    /// </summary>
    public sealed record SearchSideInfo(
        string? SearchText,
        string? ObjectType,
        string? Color,
        string? Brand,
        List<string> Tags);

    /// <summary>
    /// Candidate-side facts available to the explanation provider, taken
    /// directly from the matched <see cref="Report"/>'s own AI-classified
    /// attributes. Raw (not normalized/lower-cased) so the explanation reads
    /// naturally.
    /// </summary>
    public sealed record CandidateSideInfo(
        string? Description,
        string? ObjectType,
        string? Color,
        string? Brand,
        List<string> Tags);

    /// <summary>
    /// The already-finalized scoring facts available to the explanation
    /// provider. These are read-only outputs of the deterministic scoring
    /// pipeline - the provider must treat <see cref="FinalScore"/> as fixed
    /// and never recompute it.
    /// </summary>
    public sealed record ScoringSideInfo(
        double TextSimilarity,
        double ImageSimilarity,
        bool ObjectTypeMatched,
        bool ColorMatched,
        bool BrandMatched,
        List<string> CommonTags,
        double Penalty,
        double FinalScore);

    /// <summary>
    /// The complete structured, human-inspectable explanation model for one
    /// candidate match: search side, candidate side, and scoring side facts.
    /// This is the only information <see cref="MatchExplanationGenerator"/>
    /// ever reads to build <c>MatchExplanation</c>. Contains no embeddings,
    /// vectors, or other internal implementation details.
    /// </summary>
    public sealed record ReasonSummary(
        SearchSideInfo Search,
        CandidateSideInfo Candidate,
        ScoringSideInfo Scoring);
}