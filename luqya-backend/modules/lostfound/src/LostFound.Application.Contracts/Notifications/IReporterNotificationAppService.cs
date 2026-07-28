using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Notifications.Dtos;

namespace LostFound.Notifications
{
    public interface INotificationAppService : IApplicationService
    {
        Task<PagedResultDto<NotificationDto>> GetListAsync(Guid reporterId, PagedAndSortedResultRequestDto input);
        Task<NotificationDto> CreateAsync(CreateNotificationDto input);
        Task MarkAsReadAsync(Guid id);
    }
}
