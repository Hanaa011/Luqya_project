using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using LostFound.Reports;
using LostFound.Matching;

namespace LostFound.AI
{
    public class AiMatchingService : IAiMatchingService, ITransientDependency
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IReportRepository _reportRepository;
        private readonly ILogger<AiMatchingService> _logger;

        public AiMatchingService(
            IEmbeddingProvider embeddingProvider,
            IReportRepository reportRepository,
            ILogger<AiMatchingService> logger)
        {
            _embeddingProvider = embeddingProvider;
            _reportRepository = reportRepository;
            _logger = logger;
        }

        public async Task<List<RankedReportResult>> FindSimilarReportsAsync(
            string? searchText,
            byte[]? imageBytes,
            ReportType? type,
            int maxResults = 10)
        {
            _logger.LogInformation("========== AI Matching Started ==========");
            _logger.LogInformation("Search Text: {Text}", searchText);
            _logger.LogInformation("Has Image: {HasImage}", imageBytes != null);

            //------------------------------------------
            // Query Embeddings
            //------------------------------------------
            float[]? queryTextEmbedding = null;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                queryTextEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(searchText);

                _logger.LogInformation(
                    "Query Text Embedding Length = {Length}",
                    queryTextEmbedding.Length);
            }

            float[]? queryImageEmbedding = null;

            if (imageBytes != null)
            {
                queryImageEmbedding =
                    await _embeddingProvider.GenerateImageEmbeddingAsync(imageBytes);

                _logger.LogInformation(
                    "Query Image Embedding Length = {Length}",
                    queryImageEmbedding.Length);
            }

            //------------------------------------------
            // Load Reports
            //------------------------------------------
            var candidates = await _reportRepository.GetSearchableReportsAsync(type);

            _logger.LogInformation(
                "Repository returned {Count} searchable report(s).",
                candidates.Count);

            var results = new List<RankedReportResult>();

            foreach (var r in candidates)
            {
                var textEmbedding = r.GetEmbeddingVector();
                var imageEmbedding = r.GetImageEmbeddingVector();

                _logger.LogInformation("------------------------------------");
                _logger.LogInformation("Report: {Id}", r.Id);
                _logger.LogInformation("Description: {Description}", r.Description);

                _logger.LogInformation(
                    "Stored Text Embedding Length: {Length}",
                    textEmbedding?.Length ?? 0);

                _logger.LogInformation(
                    "Stored Image Embedding Length: {Length}",
                    imageEmbedding?.Length ?? 0);

                double? textScore = null;

                if (queryTextEmbedding != null &&
                    textEmbedding != null &&
                    textEmbedding.Length > 0)
                {
                    textScore = CosineSimilarityCalculator.CalculatePercentage(
                        queryTextEmbedding,
                        textEmbedding);

                    _logger.LogInformation(
                        "Text Score = {Score}",
                        textScore);
                }

                double? imageScore = null;

                if (queryImageEmbedding != null &&
                    imageEmbedding != null &&
                    imageEmbedding.Length > 0)
                {
                    imageScore = CosineSimilarityCalculator.CalculatePercentage(
                        queryImageEmbedding,
                        imageEmbedding);

                    _logger.LogInformation(
                        "Image Score = {Score}",
                        imageScore);
                }

                double combined = (textScore, imageScore) switch
                {
                    (not null, not null) => textScore.Value * 0.6 + imageScore.Value * 0.4,
                    (not null, null) => textScore.Value,
                    (null, not null) => imageScore.Value,
                    _ => 0
                };

                _logger.LogInformation(
                    "Combined Score = {Score}",
                    combined);

                if (combined <= 0)
                {
                    _logger.LogInformation("Skipped.");
                    continue;
                }

                _logger.LogInformation("Added to results.");

                results.Add(new RankedReportResult
                {
                    ReportId = r.Id,
                    Description = r.Description,
                    Color = r.Color,
                    AiObjectType = r.AiObjectType,
                    ScorePercentage = combined
                });
            }

            _logger.LogInformation(
                "Final Results Count = {Count}",
                results.Count);

            _logger.LogInformation("========== AI Matching Finished ==========");

            return results
                .OrderByDescending(x => x.ScorePercentage)
                .Take(maxResults)
                .ToList();
        }
    }
}