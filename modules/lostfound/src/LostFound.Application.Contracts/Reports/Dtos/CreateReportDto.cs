using System;
using System.ComponentModel.DataAnnotations;
using LostFound.Reports;
using LostFound.Reporters;

namespace LostFound.Reports.Dtos
{
    public class CreateReportDto
    {
        // Structured Location dropdown - unchanged, still required.
        [Required]
        public Guid LocationId { get; set; }

        // Optional "Specific Location" free-text field next to the dropdown.
        [StringLength(ReportConsts.MaxLocationDetailsLength)]
        public string? LocationDetails { get; set; }

        [Required]
        public ReportType Type { get; set; }

        public string? Description { get; set; }

        public DateTime? LostFoundDate { get; set; }

        [StringLength(ReportConsts.MaxImagePathLength)]
        public string? ImagePath { get; set; }

        public bool IsItemWithFinder { get; set; }

        [StringLength(ReportConsts.MaxPickupLocationLength)]
        public string? PickupLocation { get; set; }

        // ---- Guest-only contact info (ignored for authenticated users) ----
        public string? ReporterName { get; set; }
        public string? ReporterPhone { get; set; }
        public string? ReporterEmail { get; set; }
        public PreferredContactType PreferredContact { get; set; } = PreferredContactType.Phone;
    }
}
