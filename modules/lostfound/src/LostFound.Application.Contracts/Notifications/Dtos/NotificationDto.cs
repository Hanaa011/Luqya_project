using System;
using Volo.Abp.Application.Dtos;

namespace LostFound.Notifications.Dtos
{
    public class NotificationDto : AuditedEntityDto<Guid>
    {
        public Guid ReporterId { get; set; }
        public Guid ReportId { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public bool IsRead { get; set; }
    }
}
