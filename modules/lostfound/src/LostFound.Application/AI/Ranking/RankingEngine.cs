using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LostFound.AI.Concepts;
using LostFound.AI.Configuration;
using LostFound.AI.Core;
using LostFound.AI.Ontology;
using LostFound.AI.Query;
using LostFound.AI.Retrieval;

namespace LostFound.AI.Ranking
{
    /// <summary>
    /// Orchestrates the full pipeline: Feature Extraction -&gt; Score
    /// Normalization -&gt; Weighted Ranking -&gt; Metadata Penalties (PHASE-
    /// VALIDATION-06) -&gt; Cross Encoder (optional) -&gt; Confidence
    /// Calibration -&gt; Explanation Generation -&gt; Final Ranked Results,
    /// the spec's pipeline diagram plus the metadata-penalty stage this
    /// validation phase adds. Consumes Phase 2B Part 2's candidate set;
    /// produces the final ranked, explainable, calibrated result list Phase
    /// 2B Part 4 wires into AiMatchingService.
    /// </summary>
    internal sealed class RankingEngine(
        IFeatureExtractor featureExtractor,
        IScoreNormalizer scoreNormalizer,
        IWeightProvider weightProvider,
        ICrossEncoder crossEncoder,
        ILearningToRankEngine learningToRankEngine,
        IConfidenceCalibrator confidenceCalibrator,
        IExplanationGenerator explanationGenerator,
        IAIFallbackOrchestrator fallbackOrchestrator,
        IObjectTypeCompatibilityService objectTypeCompatibilityService,
        IConceptResolver conceptResolver,
        IEmbeddingEngine embeddingEngine,
        IOptions<RankingOptions> options,
        IOptions<HybridPipelineOptions> hybridOptions,
        ILogger<RankingEngine> logger) : IRankingEngine
    {
        public async Task<RankingResult> RankAsync(
            IReadOnlyList<FusedCandidate> candidates,
            IReadOnlyDictionary<System.Guid, SearchableReport> reportsById,
            SemanticQuery query,
            CancellationToken cancellationToken = default)
        {
            var totalStopwatch = Stopwatch.StartNew();

            // ---- Feature Extraction ----
            var featureStopwatch = Stopwatch.StartNew();
            var rawFeaturesByReport = new Dictionary<System.Guid, RankingFeatures>();

            foreach (var candidate in candidates)
            {
                if (!reportsById.TryGetValue(candidate.ReportId, out var report))
                {
                    continue;
                }

                rawFeaturesByReport[candidate.ReportId] = await featureExtractor.ExtractAsync(candidate, report, query, cancellationToken);
            }

            var featureExtractionMs = featureStopwatch.ElapsedMilliseconds;

            // ---- Score Normalization + Weighted Ranking (Learning-to-Rank) ----
            var weights = weightProvider.GetWeights();
            var normalizedFeaturesByReport = new Dictionary<System.Guid, RankingFeatures>();
            var rawScoresByReport = new Dictionary<System.Guid, double>();

            // ---- Metadata Penalties (PHASE-VALIDATION-06) ----
            // Computed and folded into the RAW score here, before sorting -
            // not as a post-hoc adjustment to the already-sorted/truncated
            // display confidence (see SemanticSearchOrchestrator's previous
            // ApplyCompatibilityAdjustment, now removed). This is what
            // actually pushes an unrelated object (e.g. "Car Keys" against a
            // "Laptop" query) down in the real result ORDER, not just in the
            // percentage shown for a slot it already occupies.
            var compatibilityByReport = new Dictionary<System.Guid, ObjectTypeCompatibility>();
            var penaltyByReport = new Dictionary<System.Guid, MetadataPenaltyBreakdown>();

            foreach (var (reportId, rawFeatures) in rawFeaturesByReport)
            {
                var normalized = scoreNormalizer.Normalize(rawFeatures);
                normalizedFeaturesByReport[reportId] = normalized;
                var baseScore = learningToRankEngine.Score(normalized, weights);

                var report = reportsById[reportId];

                var compatibility = await objectTypeCompatibilityService.ClassifyAsync(
                    query.ResolvedConceptId, report.ObjectType, query.LanguageCode, cancellationToken);
                compatibilityByReport[reportId] = compatibility;

                var penalty = await ComputeMetadataPenaltyAsync(query, report, compatibility, hybridOptions.Value, cancellationToken);
                penaltyByReport[reportId] = penalty;

                rawScoresByReport[reportId] = System.Math.Max(0.0, baseScore - penalty.Total);
            }

            // ---- Cross Encoder (optional) ----
            var rerankStopwatch = Stopwatch.StartNew();
            var topN = candidates
                .Where(c => rawScoresByReport.ContainsKey(c.ReportId))
                .OrderByDescending(c => rawScoresByReport[c.ReportId])
                .Take(options.Value.CrossEncoderTopN)
                .ToList();

            var crossEncoderStatus = await crossEncoder.GetStatusAsync(cancellationToken);
            var crossEncoderUsed = crossEncoderStatus == CrossEncoderStatus.Available;

            if (crossEncoderUsed)
            {
                // A real cross-encoder would return re-scored candidates for
                // the caller to fold back into rawScoresByReport;
                // NullCrossEncoder passes the input through unchanged, so
                // there is nothing to fold back today.
                await crossEncoder.RerankAsync(topN, query, cancellationToken);
            }

            var rerankingMs = rerankStopwatch.ElapsedMilliseconds;

            // ---- Confidence Calibration + Explanation Generation ----
            var explanationStopwatch = Stopwatch.StartNew();
            var results = new List<RankedResult>();

            foreach (var (reportId, rawScore) in rawScoresByReport)
            {
                var normalizedFeatures = normalizedFeaturesByReport[reportId];
                var contributingSignals = CountContributingSignals(normalizedFeatures);
                var confidence = confidenceCalibrator.Calibrate(rawScore, normalizedFeatures, contributingSignals);
                var explanation = explanationGenerator.Generate(normalizedFeatures, weights, confidence, query.LanguageCode);
                var compatibility = compatibilityByReport[reportId];

                results.Add(new RankedResult(reportId, confidence, normalizedFeatures, explanation, compatibility));
            }

            var explanationGenerationMs = explanationStopwatch.ElapsedMilliseconds;
            var ranked = results.OrderByDescending(r => r.Confidence).ToList();

            // Explainable per-result score breakdown (PHASE-VALIDATION-06
            // Logging requirement) - only the top results actually surfaced
            // to the caller, to keep log volume proportional to what a user
            // can see rather than the whole (overfetched) candidate set.
            foreach (var result in ranked.Take(10))
            {
                LogExplainableBreakdown(result, weights, penaltyByReport[result.ReportId]);
            }

            var diagnostics = new RankingDiagnostics(
                normalizedFeaturesByReport,
                featureExtractionMs,
                rerankingMs,
                totalStopwatch.ElapsedMilliseconds,
                ranked.Select(r => r.Confidence).ToList(),
                FallbackTier.HybridRanking,
                explanationGenerationMs);

            var tier = fallbackOrchestrator.DetermineTier(diagnostics, embeddingEngine.EngineName, crossEncoderUsed);
            diagnostics = diagnostics with { FallbackTier = tier };

            logger.LogInformation(
                "Ranked {Count} candidate(s) in {Elapsed}ms (fallback tier: {Tier}).",
                ranked.Count, totalStopwatch.ElapsedMilliseconds, tier);

            return new RankingResult(ranked, diagnostics);
        }

        private static int CountContributingSignals(RankingFeatures features)
        {
            var values = new[]
            {
                features.EmbeddingSimilarity, features.MetadataEmbeddingSimilarity, features.Bm25Score, features.KnowledgeGraphSimilarity, features.ObjectTypeSimilarity,
                features.CategorySimilarity, features.BrandSimilarity, features.ColorSimilarity, features.MaterialSimilarity,
                features.LocationSimilarity, features.TimeProximity, features.AliasMatch, features.ExactMatch
            };

            return values.Count(v => v > 0);
        }

        // PHASE-VALIDATION-06: object-type compatibility (ontology-driven,
        // via IObjectTypeCompatibilityService - already existed, just never
        // reached the actual score before) plus explicit Category/Brand/
        // Color mismatch penalties. A mismatch only fires when the query
        // itself recognized an entity of that type (IEntityRecognizer) AND
        // the candidate has a conflicting value - a report simply missing a
        // field is "unknown", not "wrong", and is never penalized for it.
        private async Task<MetadataPenaltyBreakdown> ComputeMetadataPenaltyAsync(
            SemanticQuery query, SearchableReport report, ObjectTypeCompatibility compatibility, HybridPipelineOptions options,
            CancellationToken cancellationToken)
        {
            var objectTypePenalty = compatibility switch
            {
                ObjectTypeCompatibility.RelatedCluster => options.RelatedClusterConfidencePenalty,
                ObjectTypeCompatibility.UnrelatedCluster => options.UnrelatedClusterConfidencePenalty,
                ObjectTypeCompatibility.Same => 0.0,
                // Real E2E finding (Search<->Matching Ranking Unification
                // validation): ObjectTypeCompatibility.Unknown is returned
                // whenever EITHER side's object type fails to resolve to a
                // knowledge-graph concept - which, for a real Arabic
                // dataset, turned out to include common categories the seed
                // ontology simply never got a concept for (e.g. "Jacket"
                // never resolves at all, so a Jacket-vs-Wallet pair silently
                // received ZERO object-type penalty and matched at 94.7%
                // confidence - proven via real RankExplain logs showing
                // Compatibility="Unknown" for every candidate against every
                // Jacket-based query, including the genuine same-type
                // candidate). Unknown does NOT distinguish "no ontology data
                // for either type" from "no data because one side has no
                // object type at all" (Report.HasObjectType's own "unknown
                // is not wrong" principle, applied at the ontology layer).
                // Only in the case where BOTH sides genuinely have a
                // non-empty, DIFFERENT object-type string is a real
                // ontology gap - not missing data - the explanation, and
                // only then does the cheap, ontology-independent
                // ObjectTypeRelationship string-clustering fallback (the
                // same table AiMatchingService's legacy scoring path
                // already relies on for this exact purpose) get a chance to
                // catch what the knowledge graph missed.
                _ => ComputeStringFallbackPenalty(query, report, options)
            };

            var categoryPenalty = await FieldMismatchPenaltyAsync(query, EntityType.Category, report.CategoryName, options.CategoryMismatchPenalty, cancellationToken);
            var brandPenalty = await FieldMismatchPenaltyAsync(query, EntityType.Brand, report.Brand, options.BrandMismatchPenalty, cancellationToken);
            var colorPenalty = await FieldMismatchPenaltyAsync(query, EntityType.Color, report.Color, options.ColorMismatchPenalty, cancellationToken);

            return new MetadataPenaltyBreakdown(objectTypePenalty, categoryPenalty, brandPenalty, colorPenalty);
        }

        // Cheap, ontology-independent safety net for ObjectTypeCompatibility.
        // Unknown - see the call site's remarks for why this is needed and
        // when it engages. Mirrors AiMatchingService.CalculatePenalty's own
        // use of the same LostFound.AI.ObjectTypeRelationship table for the
        // legacy (non-hybrid) scoring path, so "what counts as unrelated"
        // stays defined in exactly one place across both pipelines.
        private static double ComputeStringFallbackPenalty(SemanticQuery query, SearchableReport report, HybridPipelineOptions options)
        {
            if (string.IsNullOrWhiteSpace(report.ObjectType))
            {
                return 0.0;
            }

            var queryObjectEntity = query.Entities.FirstOrDefault(e =>
                e.Type == EntityType.Object && query.NormalizedText.Contains(e.Value, StringComparison.OrdinalIgnoreCase));
            if (queryObjectEntity == null)
            {
                return 0.0;
            }

            var normalizedQueryType = LostFound.Reports.Report.NormalizeAttribute(queryObjectEntity.Value);
            var normalizedCandidateType = LostFound.Reports.Report.NormalizeAttribute(report.ObjectType);

            if (normalizedQueryType.Length == 0 || normalizedQueryType == normalizedCandidateType)
            {
                return 0.0;
            }

            return LostFound.AI.ObjectTypeRelationship.Classify(normalizedCandidateType, normalizedQueryType) switch
            {
                LostFound.AI.ObjectTypeRelationship.Relationship.RelatedCluster => options.RelatedClusterConfidencePenalty,
                LostFound.AI.ObjectTypeRelationship.Relationship.UnrelatedCluster => options.UnrelatedClusterConfidencePenalty,
                _ => 0.0
            };
        }

        // PHASE-VALIDATION-08: two fixes on top of the original literal
        // comparison.
        //
        // (1) Genuineness guard - only the first recognized entity of this
        // type used to be trusted, with no check that it was actually typed
        // by the user. ISpellCorrectionService can hallucinate an entity
        // from an unrelated real word (e.g. "work" edit-distance-corrected
        // into the Brand "Apple") - see the Hybrid-Semantic-Search-Evaluation
        // report. An entity absent from the query's own NormalizedText is
        // never trustworthy enough to penalize an otherwise-correct
        // candidate, mirroring the guard LocalClassificationProvider.
        // GetGenuineEntity already applies during classification.
        //
        // (2) Concept-identity comparison (via ConceptTextMatcher), additive
        // to the original literal check - "Canon"/"كانون" must not be
        // penalized as a mismatch against each other just because they are
        // different strings for the same ontology concept.
        private async Task<double> FieldMismatchPenaltyAsync(
            SemanticQuery query, EntityType type, string? reportValue, double penaltyValue, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reportValue))
            {
                return 0.0;
            }

            var entity = query.Entities.FirstOrDefault(e =>
                e.Type == type && query.NormalizedText.Contains(e.Value, StringComparison.OrdinalIgnoreCase));
            if (entity == null)
            {
                return 0.0;
            }

            var same = await ConceptTextMatcher.AreSameAsync(
                entity.Value, reportValue, query.LanguageCode, conceptResolver, cancellationToken);
            return same ? 0.0 : penaltyValue;
        }

        // Explainable ranking log (PHASE-VALIDATION-06 Logging requirement):
        // every top result states which signals contributed, by how much,
        // and what penalty (if any) was subtracted and why - not just a bare
        // final percentage.
        private void LogExplainableBreakdown(RankedResult result, IReadOnlyDictionary<string, double> weights, MetadataPenaltyBreakdown penalty)
        {
            double WeightedContribution(string featureName, double normalizedValue) =>
                System.Math.Round(normalizedValue * weights.GetValueOrDefault(featureName, 0), 1);

            var f = result.Features;

            logger.LogInformation(
                "RankExplain ReportId={ReportId}: SemanticSimilarity={Semantic:0.00}, MetadataSimilarity={MetadataSemantic:0.00}, ObjectMatch=+{ObjectMatch:0.0}, " +
                "CategoryMatch=+{CategoryMatch:0.0}, BrandMatch=+{BrandMatch:0.0}, ColorMatch=+{ColorMatch:0.0}, " +
                "Compatibility={Compatibility}, ObjectTypePenalty=-{ObjectTypePenalty:0.0}, CategoryPenalty=-{CategoryPenalty:0.0}, " +
                "BrandPenalty=-{BrandPenalty:0.0}, ColorPenalty=-{ColorPenalty:0.0}, TotalPenalty=-{TotalPenalty:0.0}, FinalScore={FinalScore:0.0}",
                result.ReportId,
                f.EmbeddingSimilarity,
                f.MetadataEmbeddingSimilarity,
                WeightedContribution("ObjectTypeSimilarity", f.ObjectTypeSimilarity),
                WeightedContribution("CategorySimilarity", f.CategorySimilarity),
                WeightedContribution("BrandSimilarity", f.BrandSimilarity),
                WeightedContribution("ColorSimilarity", f.ColorSimilarity),
                result.Compatibility,
                penalty.ObjectTypePenalty,
                penalty.CategoryPenalty,
                penalty.BrandPenalty,
                penalty.ColorPenalty,
                penalty.Total,
                result.Confidence);
        }

        private readonly record struct MetadataPenaltyBreakdown(
            double ObjectTypePenalty, double CategoryPenalty, double BrandPenalty, double ColorPenalty)
        {
            public double Total => ObjectTypePenalty + CategoryPenalty + BrandPenalty + ColorPenalty;
        }
    }
}
