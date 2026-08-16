using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Core;

namespace LostFound.AI.Concepts
{
    /// <remarks>
    /// Real E2E finding (AI-Classification-Ontology-Vocabulary-Architecture
    /// investigation): every ranking-relevant consumer of concept resolution
    /// - QueryPipeline Stage 9, FeatureExtractor's object-type/category
    /// bonus, ObjectTypeCompatibilityService's penalty/Compatibility
    /// classification - already funnels through this one class. It used to
    /// do exact alias/synonym lookup ONLY: any AiObjectType string the AI
    /// classifier produced (Gemini, the local LLM fallback, or a user's own
    /// search text) that wasn't already a literal, curated ontology surface
    /// form resolved to nothing, silently disabling every downstream
    /// metadata-ranking signal for it. Measured empirically against this
    /// workspace's real data: 16 of 31 (51.6%) of every distinct AiObjectType
    /// value actually produced had no matching concept at all - see the
    /// architecture report for the full investigation.
    ///
    /// A semantic-similarity fallback tier (embed the input, compare by
    /// cosine similarity against every known concept's canonical-name
    /// embedding) already existed - but only privately inside
    /// LocalClassificationProvider, benefiting nothing else. Moving it HERE,
    /// once, means every one of the consumers above gets it automatically
    /// with no code change on their end (they all already depend on
    /// IConceptResolver), and the local classification pipeline and the
    /// ranking pipeline are now provably using the exact same resolution
    /// path - they cannot silently drift apart the way a second, separately
    /// maintained implementation could. This is also why the fallback belongs
    /// on the INTERFACE's sole implementation rather than as a decorator:
    /// there is exactly one production implementation of IConceptResolver in
    /// this codebase, and a decorator would just be a second class for no
    /// architectural benefit.
    /// </remarks>
    internal sealed class ConceptResolver(
        IConceptNormalizer normalizer,
        IAliasResolver aliasResolver,
        IConceptRepository conceptRepository,
        IEmbeddingEngine embeddingEngine,
        ILogger<ConceptResolver> logger) : IConceptResolver
    {
        // Raw cosine similarity (not the (cosine+1)/2*100 percentage) below
        // which a nearest-concept match is discarded as noise rather than
        // trusted. Unchanged from its original calibration (PHASE-VALIDATION-06/
        // PHASE-VALIDATION-03's Semantic Quality Analysis): unrelated
        // same-language SENTENCE pairs from this workspace's embedding model
        // land around 0.5-0.62 raw cosine, so 0.68 sits safely above that
        // noise floor. That calibration does NOT hold for single-word input -
        // see MinTokenCountForSemanticFallback below.
        private const double MinSemanticSimilarity = 0.68;

        // Real E2E finding (unified concept-resolution implementation
        // validation): moving the semantic-similarity fallback from
        // per-report classification (always a whole description - multiple
        // words/a real sentence) into the shared ConceptResolver exposed it
        // to a genuinely different input distribution it was never
        // calibrated against - single, bare words (a candidate's own short
        // AiObjectType string, e.g. "Box", "Bike", "Book"). Measured live,
        // repeatedly, against the real embedding model this workspace uses:
        // single-word inputs produced near-1.0 cosine similarity against
        // semantically UNRELATED single-word concept names ("book"->"Samsung"
        // at 0.9999, "bike"->"Visa" at 1.0000, "box"->"Light" at 0.9999) -
        // scores far HIGHER than the one plausible-looking single-word match
        // found ("jacket"->"Clothing" at 0.70), which means no threshold can
        // separate the genuine matches from the false ones for this input
        // class - the false ones score higher than the true one. The same
        // model, given a genuine multi-word phrase with no real ontology
        // match ("Vacuum Cleaner"), correctly and repeatedly returned no
        // match at all (never exceeded 0.68). The failure is specific to
        // single-token input, not semantic matching in general, so the fix
        // is to require more than one token before trusting this fallback -
        // not to duplicate its logic somewhere else, and not to abandon it
        // for the many real cases (whole descriptions, multi-word object
        // types like "Bank Card"/"ID Card") where it demonstrably works.
        private const int MinTokenCountForSemanticFallback = 2;

        // See LocalClassificationProvider's original remarks (moved from
        // there): bounds the one-time cost of embedding every concept's
        // canonical name into the lazily-built index below. Beyond this cap,
        // the semantic fallback is skipped (logged, not thrown) rather than
        // silently scaling to an unbounded per-process embedding cost; exact
        // alias/synonym resolution remains fully active regardless.
        private const int MaxConceptsForEmbeddingIndex = 3000;

        private readonly SemaphoreSlim _indexLock = new(1, 1);
        private IReadOnlyList<ConceptEmbeddingEntry>? _conceptEmbeddingIndex;

        public async Task<Concept?> ResolveAsync(string text, string languageCode, CancellationToken cancellationToken = default)
        {
            var normalized = normalizer.Normalize(text, languageCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var conceptId = await aliasResolver.ResolveAsync(normalized, languageCode, cancellationToken);
            if (conceptId != null)
            {
                return await conceptRepository.GetByIdAsync(conceptId.Value, cancellationToken);
            }

            // Real E2E finding (Multi-Representation Embedding validation,
            // V01-Samsung-S23 investigation): IAliasResolver's index is keyed
            // by (normalizedText, language) - see InMemoryAliasResolver's own
            // remarks. Every ranking-relevant caller resolves a CANDIDATE'S
            // OWN AiObjectType/Color/AiBrand text using the QUERY's detected
            // language (FeatureExtractor.ObjectTypeMatchesResolvedConceptAsync,
            // ObjectTypeCompatibilityService.ClassifyAsync,
            // RankingEngine.FieldMismatchPenaltyAsync via ConceptTextMatcher) -
            // but that classification text is reliably ENGLISH regardless of
            // the source report's own language, confirmed empirically across
            // every classification provider tested this project
            // (Gemini/OpenAI/Local LLM/rule-based all consistently return
            // English words like "Smartphone"/"Casio"/"Black" even for
            // purely-Arabic source descriptions - see the classification
            // benchmark reports). Resolving "Smartphone" under language="ar"
            // therefore always misses the exact-match index entirely (it was
            // only ever indexed under "en"), and - being one token - is ALSO
            // barred from the semantic-fallback tier below by
            // MinTokenCountForSemanticFallback, so it silently resolved to
            // nothing 100% of the time. Live RankExplain evidence: a real
            // Arabic search for a Samsung Galaxy S23 phone showed
            // Compatibility=Unknown and ObjectMatch=+0.0 for every candidate
            // INCLUDING the correct one, so the ranking engine's single
            // strongest discriminating signal (the +/-40 point object-type
            // bonus/penalty) never fired for either the true match or the
            // wrong-model/wrong-object-type distractors that outranked it.
            // A second, English-scoped exact-match attempt - tried only when
            // the caller's own language isn't already English and the first
            // attempt found nothing - fixes this at the single shared root
            // (every caller above benefits with no change of its own)
            // without weakening resolution for genuinely non-English text:
            // it is still an EXACT alias/synonym match, just tried under a
            // second, empirically-justified language before falling through
            // to the much less precise semantic-similarity tier.
            if (!string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedEnglish = normalizer.Normalize(text, "en");
                if (!string.IsNullOrWhiteSpace(normalizedEnglish))
                {
                    var englishConceptId = await aliasResolver.ResolveAsync(normalizedEnglish, "en", cancellationToken);
                    if (englishConceptId != null)
                    {
                        return await conceptRepository.GetByIdAsync(englishConceptId.Value, cancellationToken);
                    }
                }
            }

            // Real E2E finding (context-enrichment investigation, following
            // up on the single-word semantic-fallback limitation): tried,
            // and reverted, embedding the caller's full context text (e.g. a
            // report's Description) instead of the bare 'text' when
            // available, hoping the original technique's proven-reliable
            // sentence-length input shape would resolve single-word gaps
            // like "Jacket"/"Box" more accurately. Tested against every real
            // candidate available (11 real reports, 9 different search
            // queries): context-based resolution fired exactly once and was
            // wrong both times it was observed - "Necklace" (context: "lost
            // my عقد ذهبي gold necklace near cafeteria") resolved to
            // "Houghton-Butcher Manufacturing Co." (an unrelated, obscure
            // imported antique-camera-brand concept), reproduced identically
            // across repeated runs. Zero of the other ~10 candidates tested
            // (Jacket, Textbook, Box, Shoe, Umbrella, Wristwatch, Bike, Book)
            // got a correct match via context either. Root cause: the
            // problem was never really "single words embed unreliably" in
            // isolation - it's that comparing ANY input, short or
            // sentence-length, against a SPARSE ~111-concept ontology via a
            // fixed similarity threshold will occasionally surface a
            // spuriously-qualifying "nearest available" concept when the
            // true match simply isn't in the ontology at all, and certain
            // imported concepts (short/generic-sounding brand names) appear
            // to act as accidental attractors for otherwise-unrelated text
            // regardless of input length. Longer input did not fix this,
            // it only changed which unrelated concept got attracted. Kept
            // the conservative, already-validated behavior (semantic
            // fallback stays exact-match-first, sentence-length-only,
            // context-independent) rather than ship a change proven not to
            // outperform it - see the AI-Classification-Ontology-Vocabulary
            // context-enrichment investigation report for the full
            // evidence.
            var semanticMatch = await TryResolveBySemanticSimilarityAsyncSafe(normalized, cancellationToken);
            if (semanticMatch != null)
            {
                logger.LogInformation(
                    "ConceptResolver: '{Text}' ({Language}) had no exact ontology match; resolved via semantic " +
                    "similarity to concept '{Concept}' (similarity={Similarity:0.00}).",
                    text, languageCode, semanticMatch.Value.Concept.CanonicalName, semanticMatch.Value.Similarity);
                return semanticMatch.Value.Concept;
            }

            // Unresolved-concept telemetry: neither exact nor semantic
            // resolution found anything. Per the architecture report's
            // recommendation, this is surfaced (not silently discarded) so a
            // genuine, real ontology gap can be reviewed and closed through
            // the existing curated import pipeline (IImportCoordinator),
            // rather than the alternative of auto-creating a low-quality
            // concept stub for it - the ontology's own design principle is
            // explicit that it must never become "a simple dictionary".
            // Deliberately a structured log line, not a new database table:
            // this reuses infrastructure (Serilog, already flowing to file +
            // console in every environment this application runs in) instead
            // of adding a new schema/migration/repository to maintain for a
            // capability whose consumer is a periodic manual/curated review,
            // not another runtime code path.
            logger.LogInformation(
                "[UnresolvedConcept] Text='{Text}' Language={Language} - no exact or semantic ontology match found.",
                text, languageCode);

            return null;
        }

        // Real E2E finding (unified concept-resolution implementation
        // validation): ResolveAsync is now called far more broadly than
        // before this change - QueryPipeline, FeatureExtractor, and
        // ObjectTypeCompatibilityService all funnel through it for every
        // query and every candidate, not just per-report classification.
        // Any transient failure of the embedding engine underneath the
        // semantic-similarity fallback (provider rate-limited, network
        // error, misconfiguration) must therefore never be allowed to
        // propagate out of ResolveAsync - it would take down ranking/search
        // entirely for every caller, not just degrade one enrichment.
        // Mirrors the same "AI provider failure is a soft degrade, never a
        // hard failure" posture already applied everywhere else in this
        // pipeline (ClassificationEngine's Local-First fallback,
        // ReportMatchingBackgroundJob's image-embedding try/catch,
        // AiMatchingService's classification timeout handling) - exact
        // alias/synonym resolution (already attempted before this is ever
        // called) is completely unaffected either way.
        private async Task<(Concept Concept, double Similarity)?> TryResolveBySemanticSimilarityAsyncSafe(
            string normalizedText, CancellationToken cancellationToken)
        {
            try
            {
                return await TryResolveBySemanticSimilarityAsync(normalizedText, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "ConceptResolver: semantic-similarity fallback failed for '{Text}'; continuing with exact " +
                    "alias/synonym resolution only for this call.",
                    normalizedText);
                return null;
            }
        }

        private async Task<(Concept Concept, double Similarity)?> TryResolveBySemanticSimilarityAsync(
            string normalizedText, CancellationToken cancellationToken)
        {
            // See MinTokenCountForSemanticFallback's remarks: single-word
            // input is a proven-unreliable input class for this technique -
            // skip straight to "no semantic match" rather than risk a
            // high-confidence-looking but essentially random result.
            var tokenCount = normalizedText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            if (tokenCount < MinTokenCountForSemanticFallback)
            {
                return null;
            }

            var index = await EnsureConceptEmbeddingIndexAsync(cancellationToken);
            if (index.Count == 0)
            {
                return null;
            }

            // Concept resolution is now called far more often than when this
            // technique lived only inside per-report classification (every
            // search resolves its query text AND, for every candidate,
            // whatever object type that candidate was classified with) - the
            // same input text (e.g. a candidate's own AiObjectType, fixed
            // once it's classified) recurs across many calls. Reusing the
            // existing process-wide text->embedding cache (already shared by
            // AiMatchingService's query embedding path) avoids re-embedding
            // the same string on every call instead of building a second,
            // parallel cache here.
            if (!QueryProcessingCache.TryGetEmbedding(normalizedText, out var queryEmbedding) || queryEmbedding == null)
            {
                queryEmbedding = await embeddingEngine.GenerateEmbeddingAsync(normalizedText, cancellationToken);
                QueryProcessingCache.SetEmbedding(normalizedText, queryEmbedding);
            }

            Concept? best = null;
            var bestScore = MinSemanticSimilarity;

            foreach (var entry in index)
            {
                var score = CosineSimilarity(queryEmbedding, entry.Embedding);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry.Concept;
                }
            }

            return best == null ? null : (best, bestScore);
        }

        private async Task<IReadOnlyList<ConceptEmbeddingEntry>> EnsureConceptEmbeddingIndexAsync(CancellationToken cancellationToken)
        {
            if (_conceptEmbeddingIndex != null)
            {
                return _conceptEmbeddingIndex;
            }

            await _indexLock.WaitAsync(cancellationToken);
            try
            {
                if (_conceptEmbeddingIndex != null)
                {
                    return _conceptEmbeddingIndex;
                }

                var concepts = await conceptRepository.GetAllAsync(cancellationToken);
                var activeConcepts = concepts.Where(c => c.IsActive).ToList();

                if (activeConcepts.Count > MaxConceptsForEmbeddingIndex)
                {
                    logger.LogWarning(
                        "ConceptResolver: {Count} active concept(s) exceeds the embedding-index cap ({Cap}); " +
                        "skipping the semantic-similarity fallback for this process lifetime. Exact alias/synonym " +
                        "resolution remains fully active.",
                        activeConcepts.Count, MaxConceptsForEmbeddingIndex);

                    _conceptEmbeddingIndex = Array.Empty<ConceptEmbeddingEntry>();
                    return _conceptEmbeddingIndex;
                }

                var index = new List<ConceptEmbeddingEntry>(activeConcepts.Count);
                foreach (var concept in activeConcepts)
                {
                    if (string.IsNullOrWhiteSpace(concept.CanonicalName))
                    {
                        continue;
                    }

                    var embedding = await embeddingEngine.GenerateEmbeddingAsync(concept.CanonicalName, cancellationToken);
                    index.Add(new ConceptEmbeddingEntry(concept, embedding));
                }

                logger.LogInformation(
                    "ConceptResolver: built concept embedding index - {Count} concept(s) indexed.", index.Count);

                _conceptEmbeddingIndex = index;
                return _conceptEmbeddingIndex;
            }
            finally
            {
                _indexLock.Release();
            }
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            var length = Math.Min(a.Length, b.Length);
            if (length == 0)
            {
                return 0.0;
            }

            double dot = 0, normA = 0, normB = 0;
            for (var i = 0; i < length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA <= 0 || normB <= 0)
            {
                return 0.0;
            }

            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private readonly record struct ConceptEmbeddingEntry(Concept Concept, float[] Embedding);
    }
}
