using System;
using System.ComponentModel.DataAnnotations;
using LostFound.Reports;

namespace LostFound.Reports.Dtos
{
    public class UpdateReportDto
    {
        [Required]
        public ReportStatus Status { get; set; } = ReportStatus.Open;

        public string? Description { get; set; }

        [StringLength(ReportConsts.MaxLocationDetailsLength)]
        public string? LocationDetails { get; set; }

        public DateTime? LostFoundDate { get; set; }

        [StringLength(ReportConsts.MaxImagePathLength)]
        public string? ImagePath { get; set; }

        public bool IsItemWithFinder { get; set; }

        [StringLength(ReportConsts.MaxPickupLocationLength)]
        public string? PickupLocation { get; set; }
    }
}
