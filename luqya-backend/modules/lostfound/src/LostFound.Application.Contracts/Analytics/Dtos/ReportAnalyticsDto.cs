using System.Collections.Generic;

namespace LostFound.Analytics.Dtos
{
    public class ReportAnalyticsDto
    {
        public List<NameCountDto> TopCategories { get; set; } = new();
        public List<NameCountDto> TopObjectTypes { get; set; } = new();
        public List<NameCountDto> TopColors { get; set; } = new();
        public List<NameCountDto> TopLocations { get; set; } = new();
        public List<NameCountDto> TopBrands { get; set; } = new();
    }

    public class NameCountDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
