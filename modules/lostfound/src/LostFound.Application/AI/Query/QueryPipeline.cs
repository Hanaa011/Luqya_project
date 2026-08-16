using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Concepts;

namespace LostFound.AI.Query
{
    /// <summary>
    /// Orchestrates every pipeline stage in the order the Phase 2B Part 1
    /// spec defines. Never calls IEmbeddingEngine itself - the spec is
    /// explicit that this phase "does NOT implement retrieval or ranking";
    /// SemanticQuery.FinalSemanticText is handed off to whichever later
    /// stage generates the embedding (Phase 2B Part 4's production
    /// integration work).
    ///
    /// Every stage logs a "start" entry before it runs and an "end" entry
    /// (with elapsed milliseconds for that stage) right after it completes,
    /// so a single query can be traced end-to-end from the logs alone.
    /// </summary>
    internal sealed class QueryPipeline(
        ILanguageDetector languageDetector,
        ITextNormalizer textNormalizer,
        IMorphologyService morphologyService,
        ISpellCorrectionService spellCorrectionService,
        ITokenizer tokenizer,
        IIntentDetector intentDetector,
        IEntityRecognizer entityRecognizer,
        IConceptResolver conceptResolver,
        ISemanticExpander semanticExpander,
        ISemanticQueryBuilder queryBuilder,
        IQueryCache queryCache,
        ILogger<QueryPipeline> logger) : IQueryPipeline
    {
        public async Task<SemanticQuery> ProcessAsync(string rawText, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogInformation("QueryPipeline: pipeline START for raw query '{RawText}'.", rawText);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                logger.LogInformation("QueryPipeline: raw query is empty/whitespace; returning empty SemanticQuery. Pipeline END ({ElapsedMs} ms).", stopwatch.ElapsedMilliseconds);

                return queryBuilder.Build(
                    rawText ?? string.Empty, "en", string.Empty, string.Empty,
                    Array.Empty<string>(), Array.Empty<string>(), QueryIntent.Unknown, Array.Empty<RecognizedEntity>(),
                    null, Array.Empty<ExpandedTerm>(),
                    new QueryDiagnostics(stopwatch.ElapsedMilliseconds, null, Array.Empty<SpellCorrection>(), Array.Empty<RecognizedEntity>(), Array.Empty<ExpandedTerm>()));
            }

            // --- Stage 1: Language detection -----------------------------
            logger.LogDebug("QueryPipeline: [LanguageDetection] START.");
            var stageStopwatch = Stopwatch.StartNew();
            var languageResult = languageDetector.Detect(rawText);
            var languageCode = languageResult.LanguageCode;
            logger.LogDebug("QueryPipeline: [LanguageDetection] END - detected '{LanguageCode}' ({ElapsedMs} ms).", languageCode, stageStopwatch.ElapsedMilliseconds);

            // --- Stage 2: Text normalization -----------------------------
            logger.LogDebug("QueryPipeline: [Normalization] START.");
            stageStopwatch.Restart();
            var normalized = textNormalizer.Normalize(rawText);
            logger.LogDebug("QueryPipeline: [Normalization] END - '{Normalized}' ({ElapsedMs} ms).", normalized, stageStopwatch.ElapsedMilliseconds);

            // --- Stage 3: Tokenization -----------------------------------
            logger.LogDebug("QueryPipeline: [Tokenization] START.");
            stageStopwatch.Restart();
            var tokens = tokenizer.Tokenize(normalized);
            logger.LogDebug("QueryPipeline: [Tokenization] END - {TokenCount} token(s) ({ElapsedMs} ms).", tokens.Count, stageStopwatch.ElapsedMilliseconds);

            // --- Stage 4: Spell correction ---------------------------------
            logger.LogDebug("QueryPipeline: [SpellCorrection] START.");
            stageStopwatch.Restart();
            var corrections = await spellCorrectionService.CorrectAsync(tokens, languageCode, cancellationToken);
            var correctedTokens = ApplyCorrections(tokens, corrections);
            var correctedText = string.Join(' ', correctedTokens);
            logger.LogDebug("QueryPipeline: [SpellCorrection] END - {CorrectionCount} correction(s) applied ({ElapsedMs} ms).", corrections.Count, stageStopwatch.ElapsedMilliseconds);

            // --- Stage 5: Morphology / lemmatization ----------------------
            logger.LogDebug("QueryPipeline: [Morphology] START.");
            stageStopwatch.Restart();
            var lemmas = correctedTokens.Select(token => morphologyService.Lemmatize(token, languageCode)).ToList();
            logger.LogDebug("QueryPipeline: [Morphology] END - {LemmaCount} lemma(s) produced ({ElapsedMs} ms).", lemmas.Count, stageStopwatch.ElapsedMilliseconds);

            // --- Stage 6: Intent detection ---------------------------------
            logger.LogDebug("QueryPipeline: [IntentDetection] START.");
            stageStopwatch.Restart();
            var intentResult = intentDetector.Detect(correctedTokens, languageCode);
            logger.LogDebug("QueryPipeline: [IntentDetection] END - intent '{Intent}' ({ElapsedMs} ms).", intentResult.Intent, stageStopwatch.ElapsedMilliseconds);

            // --- Stage 7: Entity recognition --------------------------------
            logger.LogDebug("QueryPipeline: [EntityRecognition] START.");
            stageStopwatch.Restart();
            var entities = await entityRecognizer.RecognizeAsync(correctedTokens, languageCode, cancellationToken);
            logger.LogDebug("QueryPipeline: [EntityRecognition] END - {EntityCount} entity(ies) found ({ElapsedMs} ms).", entities.Count, stageStopwatch.ElapsedMilliseconds);

            // --- Stage 8: Cache lookup ---------------------------------------
            logger.LogDebug("QueryPipeline: [CacheLookup] START.");
            stageStopwatch.Restart();
            var cacheKey = await queryCache.BuildCacheKeyAsync(correctedText, languageCode, cancellationToken);
            var cacheHit = queryCache.TryGet(cacheKey, out var cached) && cached != null;
            logger.LogDebug("QueryPipeline: [CacheLookup] END - key '{CacheKey}', hit={CacheHit} ({ElapsedMs} ms).", cacheKey, cacheHit, stageStopwatch.ElapsedMilliseconds);

            if (cacheHit)
            {
                logger.LogInformation("QueryPipeline: cache hit for '{RawText}'; pipeline END ({ElapsedMs} ms).", rawText, stopwatch.ElapsedMilliseconds);
                return cached!;
            }

            var objectEntity = entities.FirstOrDefault(e => e.Type == EntityType.Object);

            // Real E2E finding (Search<->Matching Ranking Unification
            // validation): EntityRecognizer runs on correctedTokens (post
            // spell-correction, Stage 4), so a low-confidence edit-distance
            // "correction" of an already-valid word the ontology just
            // doesn't happen to contain (e.g. Arabic "قدم" - "foot", from
            // "كرة قدم" - football/soccer ball - corrected to "قلم" - "pen",
            // a single-character edit-distance-1 substitution) can hand
            // entity recognition an Object entity that was NEVER ACTUALLY
            // TYPED. Concept resolution below then anchors on that single,
            // fabricated word with full confidence, resolving the whole
            // query to the wrong concept (confirmed via real logs: a search
            // for a found football returned lost-pen reports as its top
            // matches). LocalClassificationProvider.GetGenuineEntity already
            // applies this exact "was this entity's value actually present
            // before spell-correction ran" check for its own caller; doing
            // it here, once, in the shared pipeline both Search and
            // Automatic Matching consume, fixes it for both instead of only
            // the report-classification path. This does not disable spell
            // correction - correctedText (used below when the guard fires,
            // and everywhere else downstream) still carries the correction;
            // only a single, isolated, unverified corrected word is barred
            // from acting as the sole concept-resolution anchor.
            var genuineObjectEntity = objectEntity != null &&
                normalized.Contains(objectEntity.Value, StringComparison.OrdinalIgnoreCase)
                    ? objectEntity
                    : null;
            var resolveText = genuineObjectEntity?.Value ?? correctedText;

            // --- Stage 9: Concept resolution ----------------------------------
            logger.LogDebug("QueryPipeline: [ConceptResolution] START - resolving '{ResolveText}'.", resolveText);
            stageStopwatch.Restart();
            Concept? resolvedConcept = null;
            try
            {
                resolvedConcept = await conceptResolver.ResolveAsync(resolveText, languageCode, cancellationToken);
                logger.LogDebug("QueryPipeline: [ConceptResolution] END - resolved concept '{ConceptId}' ({ElapsedMs} ms).", resolvedConcept?.Id, stageStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Concept resolution is an enhancement, not a hard
                // requirement - a lookup failure must not take the whole
                // query pipeline down (same "search must never fail"
                // posture as AiMatchingService's classification handling).
                logger.LogWarning(ex, "Concept resolution failed for query '{Query}'; continuing without concept expansion.", rawText);
                logger.LogDebug("QueryPipeline: [ConceptResolution] END - failed, continuing without concept ({ElapsedMs} ms).", stageStopwatch.ElapsedMilliseconds);
            }

            // --- Stage 10: Semantic expansion -----------------------------------
            logger.LogDebug("QueryPipeline: [SemanticExpansion] START.");
            stageStopwatch.Restart();
            var expandedTerms = resolvedConcept != null
                ? await semanticExpander.ExpandAsync(resolvedConcept.Id, languageCode, cancellationToken)
                : Array.Empty<ExpandedTerm>();
            logger.LogDebug("QueryPipeline: [SemanticExpansion] END - {ExpandedTermCount} term(s) ({ElapsedMs} ms).", expandedTerms.Count, stageStopwatch.ElapsedMilliseconds);

            var diagnostics = new QueryDiagnostics(stopwatch.ElapsedMilliseconds, languageCode, corrections, entities, expandedTerms);

            // --- Stage 11: Query build -------------------------------------------
            logger.LogDebug("QueryPipeline: [QueryBuild] START.");
            stageStopwatch.Restart();
            var query = queryBuilder.Build(
                rawText, languageCode, normalized, correctedText, correctedTokens, lemmas,
                intentResult.Intent, entities, resolvedConcept?.Id, expandedTerms, diagnostics);
            logger.LogDebug("QueryPipeline: [QueryBuild] END ({ElapsedMs} ms).", stageStopwatch.ElapsedMilliseconds);

            // --- Stage 12: Cache write --------------------------------------------
            logger.LogDebug("QueryPipeline: [CacheWrite] START.");
            stageStopwatch.Restart();
            queryCache.Set(cacheKey, query);
            logger.LogDebug("QueryPipeline: [CacheWrite] END ({ElapsedMs} ms).", stageStopwatch.ElapsedMilliseconds);

            logger.LogInformation("QueryPipeline: pipeline END for '{RawText}' - total {ElapsedMs} ms.", rawText, stopwatch.ElapsedMilliseconds);

            return query;
        }

        private static System.Collections.Generic.IReadOnlyList<string> ApplyCorrections(
            System.Collections.Generic.IReadOnlyList<string> tokens,
            System.Collections.Generic.IReadOnlyList<SpellCorrection> corrections)
        {
            if (corrections.Count == 0)
            {
                return tokens;
            }

            // ISpellCorrectionService.CorrectAsync iterates the token LIST,
            // not distinct words, so a word repeated in the query (e.g. "on"
            // appearing twice) can produce two SpellCorrection entries with
            // the same Original - both deterministically identical, since
            // both came from correcting the same word against the same
            // vocabulary. A plain ToDictionary throws ArgumentException on
            // the second one; GroupBy+first tolerates the duplicate (real,
            // reproduced crash - see Hybrid-Semantic-Search-Evaluation
            // report Test 4).
            var map = corrections
                .GroupBy(c => c.Original, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Corrected, StringComparer.Ordinal);
            return tokens.Select(token => map.TryGetValue(token, out var corrected) ? corrected : token).ToList();
        }
    }
}