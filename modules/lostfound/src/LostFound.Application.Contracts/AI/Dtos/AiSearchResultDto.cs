using System;
using System.Collections.Generic;
using LostFound.Reports;

namespace LostFound.AI.Dtos
{
    public class AiSearchResultDto
    {
        public Guid ReportId { get; set; }
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? AiObjectType { get; set; }

        // Phase 4 Part 3: see RankedReportResult.Type's remarks - lets the
        // frontend's claim flow know which of the searching user's own
        // reports are eligible (opposite type) without a second fetch.
        public ReportType Type { get; set; }

        // Task 3 (Phase 3 Part 3): blob name for reportImageUrl() - see
        // RankedReportResult.ImagePath's remarks. Null when the matched
        // report has no image.
        public string? ImagePath { get; set; }

        public double ScorePercentage { get; set; }
        public List<string> MatchReasons { get; set; } = new();
        public string? MatchExplanation { get; set; }
    }
}