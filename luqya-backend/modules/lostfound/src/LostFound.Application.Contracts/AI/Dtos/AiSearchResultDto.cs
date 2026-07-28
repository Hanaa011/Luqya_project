using System;

namespace LostFound.AI.Dtos
{
    public class AiSearchResultDto
    {
        public Guid ReportId { get; set; }
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? AiObjectType { get; set; }
        public double ScorePercentage { get; set; }
    }
}
