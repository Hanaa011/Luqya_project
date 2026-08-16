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
        public double ScorePercentage { get; set; }
        public List<string> MatchReasons { get; set; } = new();
        public string? MatchExplanation { get; set; }
    }
}
