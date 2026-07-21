using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using LostFound.AI;
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
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IItemClassificationProvider _classificationProvider;
        private readonly IBlobContainer<ReportImageContainer> _imageContainer;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReportMatchingBackgroundJob> _logger;

        public ReportMatchingBackgroundJob(
            IReportRepository reportRepository,
            CategoryManager categoryManager,
            MatchManager matchManager,
            IEmbeddingProvider embeddingProvider,
            IItemClassificationProvider classificationProvider,
            IBlobContainer<ReportImageContainer> imageContainer,
            IConfiguration configuration,
            ILogger<ReportMatchingBackgroundJob> logger)
        {
            _reportRepository = reportRepository;
            _categoryManager = categoryManager;
            _matchManager = matchManager;
            _embeddingProvider = embeddingProvider;
            _classificationProvider = classificationProvider;
            _imageContainer = imageContainer;
            _configuration = configuration;
            _logger = logger;
        }

        public override async Task ExecuteAsync(ReportMatchingBackgroundJobArgs args)
        {
            _logger.LogCritical("=======================================");
            _logger.LogCritical("BACKGROUND JOB STARTED");
            _logger.LogCritical("ReportId = {ReportId}", args.ReportId);
            _logger.LogCritical("=======================================");

            var report = await _reportRepository.FindAsync(args.ReportId);

            if (report == null)
            {
                _logger.LogCritical("Report NOT FOUND.");
                return;
            }

            try
            {
                _logger.LogCritical("Report Loaded");
                _logger.LogCritical("Description = {Description}", report.Description);

                byte[]? imageBytes = null;

                if (!string.IsNullOrWhiteSpace(report.ImagePath)
                    && await _imageContainer.ExistsAsync(report.ImagePath))
                {
                    imageBytes = await _imageContainer.GetAllBytesAsync(report.ImagePath);

                    _logger.LogCritical("Image Loaded. Size = {Size}", imageBytes.Length);
                }
                else
                {
                    _logger.LogCritical("No Image.");
                }

                //----------------------------------------
                // AI Classification
                //----------------------------------------

                _logger.LogCritical("Calling Classification...");

                var classification =
                    await _classificationProvider.ClassifyAsync(
                        report.Description,
                        imageBytes);

                _logger.LogCritical(
                    "Classification Done. Category={Category}, Object={Object}, Color={Color}, Brand={Brand}",
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

                    _logger.LogCritical("Category Id = {CategoryId}", categoryId);
                }

                report.ApplyAiClassification(
                    categoryId,
                    classification.ObjectType,
                    classification.Color,
                    classification.Brand,
                    classification.Tags);

                //----------------------------------------
                // TEXT EMBEDDING
                //----------------------------------------

                var text = report.BuildEmbeddingText(classification.CategoryName);

                _logger.LogCritical("Embedding Text:");
                _logger.LogCritical("{Text}", text);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogCritical("Generating Text Embedding...");

                    var embedding =
                        await _embeddingProvider.GenerateEmbeddingAsync(text);

                    _logger.LogCritical(
                        "Embedding Generated. Length = {Length}",
                        embedding.Length);

                    report.SetEmbedding(embedding);

                    _logger.LogCritical(
                        "EmbeddingJson Length = {Length}",
                        report.EmbeddingJson?.Length ?? 0);
                }

                //----------------------------------------
                // IMAGE EMBEDDING
                //----------------------------------------

                if (imageBytes != null)
                {
                    _logger.LogCritical("Generating Image Embedding...");

                    var imageEmbedding =
                        await _embeddingProvider.GenerateImageEmbeddingAsync(imageBytes);

                    _logger.LogCritical(
                        "Image Embedding Length = {Length}",
                        imageEmbedding.Length);

                    report.SetImageEmbedding(imageEmbedding);

                    _logger.LogCritical(
                        "ImageEmbeddingJson Length = {Length}",
                        report.ImageEmbeddingJson?.Length ?? 0);
                }

                //----------------------------------------
                // SAVE
                //----------------------------------------

                _logger.LogCritical("Updating Report...");

                await _reportRepository.UpdateAsync(report);

                _logger.LogCritical("Report Updated Successfully.");

                //----------------------------------------
                // MATCHING
                //----------------------------------------

                var threshold =
                    _configuration.GetValue<double?>("LostFound:AI:MatchThreshold")
                    ?? 75;

                _logger.LogCritical("Threshold = {Threshold}", threshold);

                var createdCount =
                    await _matchManager.FindAndCreateMatchesAsync(
                        report.Id,
                        threshold,
                        _embeddingProvider.ProviderName);

                _logger.LogCritical(
                    "Matching Finished. Matches Created = {Count}",
                    createdCount);

                _logger.LogCritical("BACKGROUND JOB FINISHED SUCCESSFULLY");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "BACKGROUND JOB FAILED");

                throw;
            }
        }
    }

    // Blob container marker for report images - adjust to whatever
    // container/provider (Azure/S3/FileSystem) is already configured for
    // this module's image uploads.
    public class ReportImageContainer
    {
    }
}
