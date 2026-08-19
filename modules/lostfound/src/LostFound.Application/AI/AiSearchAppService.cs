using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using LostFound.AI.Dtos;
using LostFound.Reports;

namespace LostFound.AI
{
    public class AiSearchAppService : ApplicationService, IAiSearchAppService
    {
        /// <summary>
        /// Phase 4 Part 1: hard confidence floor enforced server-side on every
        /// search result, for every query shape (text-only, image-only, and
        /// combined) - all three flow through this single method. Comparison
        /// is inclusive (&gt;= 55), and this floor is applied unconditionally;
        /// it does not depend on (and is never relaxed by) whatever the caller
        /// sends via <see cref="AiSearchInputDto.MinimumScorePercentage"/>, so
        /// the frontend can never cause a sub-55% result to be returned.
        /// </summary>
        private const double MinimumConfidenceFloorPercentage = 55.0;

        private readonly IAiMatchingService _aiMatchingService;
        private readonly IImageValidator _imageValidator;
        private readonly ILogger<AiSearchAppService> _logger;

        public AiSearchAppService(
            IAiMatchingService aiMatchingService,
            IImageValidator imageValidator,
            ILogger<AiSearchAppService> logger)
        {
            _aiMatchingService = aiMatchingService;
            _imageValidator = imageValidator;
            _logger = logger;

            _logger.LogInformation("========== AiSearchAppService Created ==========");
        }

        public async Task<List<AiSearchResultDto>> SearchAsync(AiSearchInputDto input)
        {
            _logger.LogInformation("========== SearchAsync Started ==========");

            _logger.LogInformation("Input Text: {Text}", input.Text);
            _logger.LogInformation("Has Image: {HasImage}", !string.IsNullOrWhiteSpace(input.ImageBase64));
            _logger.LogInformation("Type: {Type}", input.Type);
            _logger.LogInformation("Max Results: {MaxResults}", input.MaxResults);
            _logger.LogInformation("Minimum Score: {MinimumScore}", input.MinimumScorePercentage);

            if (string.IsNullOrWhiteSpace(input.Text) &&
                string.IsNullOrWhiteSpace(input.ImageBase64))
            {
                _logger.LogWarning("Search rejected because no text or image was provided.");

                throw new UserFriendlyException(
                    "Provide a description, an image, or both to search.");
            }

            byte[]? imageBytes = null;

            if (!string.IsNullOrWhiteSpace(input.ImageBase64))
            {
                imageBytes = Convert.FromBase64String(input.ImageBase64);
                _logger.LogInformation("Image Size: {Size} bytes", imageBytes.Length);

                // Task A2 (Luqya-System-Reference.md §20/§38 Issue #15): same
                // shared validator as ReportAppService.UploadImageAsync - an
                // oversized or unrecognized-format search image is rejected
                // here, up front, with a clear validation error, rather than
                // reaching an AI provider and failing downstream.
                var validation = _imageValidator.Validate(imageBytes);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Search image rejected: {Reason}", validation.ErrorMessage);
                    throw new UserFriendlyException(validation.ErrorMessage!);
                }
            }

            _logger.LogInformation("Calling IAiMatchingService.FindSimilarReportsAsync...");

            var results = await _aiMatchingService.FindSimilarReportsAsync(
                input.Text,
                imageBytes,
                input.Type,
                5); //input.MaxResults

            _logger.LogInformation(
                "IAiMatchingService returned {Count} result(s) before filtering.",
                results.Count);

            // Apply the confidence floor. This always runs - not gated on
            // input.MinimumScorePercentage.HasValue - because the backend
            // must never return a sub-floor result regardless of what (or
            // whether) the caller sends. See MinimumConfidenceFloorPercentage.
            results = results
                .Where(x => x.ScorePercentage >= MinimumConfidenceFloorPercentage)
                .ToList();

            _logger.LogInformation(
                "Applied minimum confidence floor: {Minimum}% -> {Count} result(s) remaining.",
                MinimumConfidenceFloorPercentage,
                results.Count);

            foreach (var result in results)
            {
                _logger.LogInformation(
                    "ReportId: {Id} | Score: {Score}% | Description: {Description}",
                    result.ReportId,
                    result.ScorePercentage,
                    result.Description);
            }

            var dto = results.Select(r => new AiSearchResultDto
            {
                ReportId = r.ReportId,
                Description = r.Description,
                Color = r.Color,
                AiObjectType = r.AiObjectType,
                Type = r.Type,
                ImagePath = r.ImagePath,
                ScorePercentage = r.ScorePercentage,
                MatchReasons = r.MatchReasons,
                MatchExplanation = r.MatchExplanation
            }).ToList();

            _logger.LogInformation("Returning {Count} DTO(s).", dto.Count);
            _logger.LogInformation("========== SearchAsync Finished ==========");

            return dto;
        }
    }
}
