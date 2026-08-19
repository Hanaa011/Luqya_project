using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LostFound.Reports;

namespace LostFound.AI
{
    // Real-time semantic search orchestration used by AiSearchAppService
    // (as opposed to MatchManager, which is the Domain-side batch/
    // event-driven matching used by the background job).
    public interface IAiMatchingService
    {
        Task<List<RankedReportResult>> FindSimilarReportsAsync(
            string? searchText,
            byte[]? imageBytes,
            ReportType? type,
            int maxResults = 10);
    }

    // Plain fields only - Application.Contracts must never reference
    // LostFound.Domain, so this is NOT the Domain Report entity.
    public class RankedReportResult
    {
        public Guid ReportId { get; set; }
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? AiObjectType { get; set; }

        // Phase 4 Part 3: needed so the frontend's claim flow can filter the
        // searching user's own reports down to the opposite type (a Found
        // result can only be claimed against a Lost report of the user's
        // own, and vice versa) without a second round-trip. Populated only
        // on the legacy scoring path (AiMatchingService.BuildSingleRankedResult) -
        // the dormant Hybrid pipeline is untouched, per this task's
        // constraints, so it still defaults to ReportType.Lost when (never,
        // today) reached.
        public ReportType Type { get; set; }

        // Task 3 (Phase 3 Part 3): the blob name (not a URL - same shape as
        // ReportDto.ImagePath) for the matched report's own photo, so search
        // results can render a thumbnail via reportImageUrl()/
        // GET api/app/report/image/{blobName} without a per-result detail
        // fetch. Null when the matched report has no image.
        public string? ImagePath { get; set; }

        public double ScorePercentage { get; set; }
        public List<string> MatchReasons { get; set; } = new();
        public string? MatchExplanation { get; set; }
    }
}
