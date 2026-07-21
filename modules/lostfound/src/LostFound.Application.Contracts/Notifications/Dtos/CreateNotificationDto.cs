using System;
using System.ComponentModel.DataAnnotations;
using LostFound.Notifications;

namespace LostFound.Notifications.Dtos
{
    public class CreateNotificationDto
    {
        [Required]
        public Guid ReporterId { get; set; }

        [Required]
        public Guid ReportId { get; set; }

        [StringLength(NotificationConsts.MaxTitleLength)]
        public string? Title { get; set; }

        public string? Message { get; set; }
    }
}
