using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using LostFound.AI.Dtos;

namespace LostFound.AI
{
    public class AiSearchAppService : ApplicationService, IAiSearchAppService
    {
        private readonly IAiMatchingService _aiMatchingService;
        private readonly ILogger<AiSearchAppService> _logger;

        public AiSearchAppService(
            IAiMatchingService aiMatchingService,
            ILogger<AiSearchAppService> logger)
        {
            _aiMatchingService = aiMatchingService;
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
            }

            _logger.LogInformation("Calling IAiMatchingService.FindSimilarReportsAsync...");

            var results = await _aiMatchingService.FindSimilarReportsAsync(
                input.Text,
                imageBytes,
                input.Type,
                input.MaxResults);

            _logger.LogInformation(
                "IAiMatchingService returned {Count} result(s) before filtering.",
                results.Count);

            // Apply minimum similarity filter
            if (input.MinimumScorePercentage.HasValue)
            {
                results = results
                    .Where(x => x.ScorePercentage >= input.MinimumScorePercentage.Value)
                    .ToList();

                _logger.LogInformation(
                    "Applied minimum score filter: {Minimum}% -> {Count} result(s) remaining.",
                    input.MinimumScorePercentage.Value,
                    results.Count);
            }

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
                ScorePercentage = r.ScorePercentage
            }).ToList();

            _logger.LogInformation("Returning {Count} DTO(s).", dto.Count);
            _logger.LogInformation("========== SearchAsync Finished ==========");

            return dto;
        }
    }
}