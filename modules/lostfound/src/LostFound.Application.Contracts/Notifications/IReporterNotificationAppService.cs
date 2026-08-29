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

        // Identity-keyed notifications (currently: missed calls) for the
        // current logged-in user - unlike GetListAsync above, this needs
        // no reporterId (there may not be one - see
        // ConversationAppService's missed-call handling) and is scoped to
        // CurrentUser.Id, not a caller-supplied id.
        Task<PagedResultDto<NotificationDto>> GetMyListAsync(PagedAndSortedResultRequestDto input);

        Task<NotificationDto> CreateAsync(CreateNotificationDto input);
        Task MarkAsReadAsync(Guid id);
    }
}
