using System;
using Volo.Abp.Application.Dtos;

namespace LostFound.Reports.Dtos
{
    public class GetReportListDto : PagedAndSortedResultRequestDto
    {
        public string? Filter { get; set; }
        public ReportType? Type { get; set; }
        public ReportStatus? Status { get; set; }
        public Guid? LocationId { get; set; }
        public Guid? ReporterId { get; set; }

        // Optional, internal/analytics use only - never required in the UI.
        public Guid? CategoryId { get; set; }
    }
}
