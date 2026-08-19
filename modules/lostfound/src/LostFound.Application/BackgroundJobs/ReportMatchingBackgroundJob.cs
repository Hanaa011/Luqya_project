using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using LostFound.AI;
using LostFound.AI.Core;
using LostFound.Reports;
using LostFound.Categories;
using LostFound.Matches;

namespace LostFound.BackgroundJobs
{
    // Workflow: Save Report -> Queue this Job -> AI Classification (resolves
    // Category) -> Generate Text Embedding -> Generate Image Embedding ->
    // Find Similar Reports -> Create Match -> Send Notification (the last
    // two steps happen inside MatchManager).
    public class ReportMatchingBackgroundJob :
        AsyncBackgroundJob<ReportMatchingBackgroundJobArgs>,
        ITransientDependency
    {
        private readonly IReportRepository _reportRepository;
        private readonly CategoryManager _categoryManager;
        private readonly MatchManager _matchManager;
        private readonly IEmbeddingEngine _embeddingEngine;
        private readonly IClassificationEngine _classificationEngine;
        private readonly IBlobContainer<ReportImageContainer> _imageContainer;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReportMatchingBackgroundJob> _logger;

        public ReportMatchingBackgroundJob(
            IReportRepository reportRepository,
            CategoryManager categoryManager,
            MatchManager matchManager,
            IEmbeddingEngine embeddingEngine,
            IClassificationEngine classificationEngine,
            IBlobContainer<ReportImageContainer> imageContainer,
            IConfiguration configuration,
            ILogger<ReportMatchingBackgroundJob> logger)
        {
            _reportRepository = reportRepository;
            _categoryManager = categoryManager;
            _matchManager = matchManager;
            _embeddingEngine = embeddingEngine;
            _classificationEngine = classificationEngine;
            _imageContainer = imageContainer;
            _configuration = configuration;
            _logger = logger;
        }

        public override async Task ExecuteAsync(ReportMatchingBackgroundJobArgs args)
        {
            _logger.LogInformation("AI matching job started for ReportId={ReportId}", args.ReportId);

            var report = await _reportRepository.FindAsync(args.ReportId);

            if (report == null)
            {
                _logger.LogWarning("AI matching job: ReportId={ReportId} not found.", args.ReportId);
                return;
            }

            try
            {
                _logger.LogDebug("Report loaded. Description={Description}", report.Description);

                byte[]? imageBytes = null;

                if (!string.IsNullOrWhiteSpace(report.ImagePath)
                    && await _imageContainer.ExistsAsync(report.ImagePath))
                {
                    imageBytes = await _imageContainer.GetAllBytesAsync(report.ImagePath);

                    _logger.LogDebug("Report image loaded. Size={Size}", imageBytes.Length);
                }

                //----------------------------------------
                // AI Classification
                //----------------------------------------

                var classification =
                    await _classificationEngine.ClassifyAsync(
                        report.Description,
                        imageBytes);

                _logger.LogInformation(
                    "Classification done for ReportId={ReportId}. Category={Category}, Object={Object}, Color={Color}, Brand={Brand}",
                    args.ReportId,
                    classification.CategoryName,
                    classification.ObjectType,
                    classification.Color,
                    classification.Brand);

                Guid? categoryId = null;

                if (!string.IsNullOrWhiteSpace(classification.CategoryName))
                {
                    var category =
                        await _categoryManager.FindOrCreateByNameAsync(
                            classification.CategoryName);

                    categoryId = category.Id;

                    _logger.LogDebug("Resolved CategoryId={CategoryId}", categoryId);
                }

                report.ApplyAiClassification(
                    categoryId,
                    classification.ObjectType,
                    classification.Color,
                    classification.Brand,
                    classification.Tags);

                //----------------------------------------
                // TEXT EMBEDDING (Description + Metadata, split - see
                // Multi-Representation-Embedding-Architecture-Analysis.md)
                //----------------------------------------

                var descriptionText = report.BuildDescriptionEmbeddingText();

                _logger.LogDebug("Description embedding text: {Text}", descriptionText);

                if (!string.IsNullOrWhiteSpace(descriptionText))
                {
                    var descriptionEmbedding =
                        await _embeddingEngine.GenerateEmbeddingAsync(descriptionText);

                    _logger.LogDebug("Description embedding generated. Length={Length}", descriptionEmbedding.Length);

                    report.SetEmbedding(descriptionEmbedding);
                }

                var metadataText = report.BuildMetadataEmbeddingText(classification.CategoryName);

                _logger.LogDebug("Metadata embedding text: {Text}", metadataText);

                if (!string.IsNullOrWhiteSpace(metadataText))
                {
                    var metadataEmbedding =
                        await _embeddingEngine.GenerateEmbeddingAsync(metadataText);

                    _logger.LogDebug("Metadata embedding generated. Length={Length}", metadataEmbedding.Length);

                    report.SetMetadataEmbedding(metadataEmbedding);
                }

                //----------------------------------------
                // IMAGE EMBEDDING
                //----------------------------------------

                // PHASE-VALIDATION-08 finding: unlike classification (which
                // now has a Local-First fallback - see ClassificationEngine)
                // and text embedding (which already runs on the local
                // BGE-M3 ONNX model), image embedding still has no local
                // path at all - GenerateImageEmbeddingAsync always calls
                // the external provider (see IEmbeddingEngine.
                // GenerateImageEmbeddingAsync's implementations; deliberately
                // left untouched here per this phase's scope - only the
                // local CLASSIFICATION implementation may be replaced, not
                // the embedding engine). Before this fix, a transient
                // failure here (observed live: a real Gemini 429) threw out
                // of the single try/catch below and aborted the ENTIRE job
                // - discarding the classification and text embedding that
                // had already been computed successfully just above, even
                // though report.ApplyAiClassification/SetEmbedding had
                // already been called on the in-memory entity, because
                // _reportRepository.UpdateAsync(report) hadn't run yet.
                // Image embedding is exactly the kind of optional
                // enrichment Validation-02 already established the rest of
                // this job should degrade gracefully around (see the class
                // remarks in ClassificationEngine) - a report with a photo
                // but no image embedding still matches on text/category/
                // color/brand, it just loses image-similarity scoring for
                // this one report until a future re-run succeeds.
                if (imageBytes != null)
                {
                    try
                    {
                        var imageEmbedding =
                            await _embeddingEngine.GenerateImageEmbeddingAsync(imageBytes);

                        _logger.LogDebug("Image embedding generated. Length={Length}", imageEmbedding.Length);

                        report.SetImageEmbedding(imageEmbedding);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Image embedding generation failed for ReportId={ReportId}; continuing without it - " +
                            "classification and text embedding computed above are not discarded.",
                            args.ReportId);
                    }
                }

                //----------------------------------------
                // SAVE
                //----------------------------------------

                await _reportRepository.UpdateAsync(report);

                _logger.LogDebug("ReportId={ReportId} updated with AI classification/embeddings.", args.ReportId);

                //----------------------------------------
                // MATCHING
                //----------------------------------------

                var threshold =
                    _configuration.GetValue<double?>("LostFound:AI:MatchThreshold")
                    ?? 75;

                var createdCount =
                    await _matchManager.FindAndCreateMatchesAsync(
                        report.Id,
                        threshold,
                        _embeddingEngine.EngineName);

                _logger.LogInformation(
                    "AI matching job finished for ReportId={ReportId}. Threshold={Threshold}, MatchesCreated={Count}",
                    args.ReportId,
                    threshold,
                    createdCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI matching job failed for ReportId={ReportId}", args.ReportId);

                throw;
            }
        }
    }
}
