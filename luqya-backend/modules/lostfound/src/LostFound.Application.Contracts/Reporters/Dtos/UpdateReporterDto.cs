using System.ComponentModel.DataAnnotations;
using LostFound.Reporters;

namespace LostFound.Reporters.Dtos
{
    // Phone is intentionally NOT updatable here - it is the guest-matching
    // key used by ReporterManager.
    public class UpdateReporterDto
    {
        [StringLength(ReporterConsts.MaxNameLength, MinimumLength = ReporterConsts.MinNameLength)]
        public string? Name { get; set; }

        [StringLength(ReporterConsts.MaxEmailLength)]
        public string? Email { get; set; }

        public PreferredContactType PreferredContact { get; set; } = PreferredContactType.Phone;
    }
}
