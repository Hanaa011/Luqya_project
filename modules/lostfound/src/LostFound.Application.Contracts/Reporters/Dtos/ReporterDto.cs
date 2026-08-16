using System;
using Volo.Abp.Application.Dtos;

namespace LostFound.Reporters.Dtos
{
    public class ReporterDto : AuditedEntityDto<Guid>
    {
        public Guid? IdentityUserId { get; set; }

        public string? Name { get; set; }

        public string Phone { get; set; } = string.Empty;

        public string? Email { get; set; }

        public PreferredContactType PreferredContact { get; set; }
    }
}
