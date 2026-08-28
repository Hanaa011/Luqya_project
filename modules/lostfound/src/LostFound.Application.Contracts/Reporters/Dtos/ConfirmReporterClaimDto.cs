using System.ComponentModel.DataAnnotations;

namespace LostFound.Reporters.Dtos
{
    public class ConfirmReporterClaimDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
}
