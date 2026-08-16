using System.ComponentModel.DataAnnotations;
using LostFound.Reporters;

namespace LostFound.Reporters.Dtos
{
    public class CreateReporterDto
    {
        [StringLength(ReporterConsts.MaxNameLength, MinimumLength = ReporterConsts.MinNameLength)]
        public string? Name { get; set; }

        [Required]
        [StringLength(ReporterConsts.MaxPhoneLength)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(ReporterConsts.MaxEmailLength)]
        public string? Email { get; set; }

        public PreferredContactType PreferredContact { get; set; } = PreferredContactType.Phone;
    }
}
