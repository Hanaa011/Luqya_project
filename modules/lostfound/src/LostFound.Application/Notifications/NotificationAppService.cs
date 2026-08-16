using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Notifications.Dtos;

namespace LostFound.Notifications
{
    public class NotificationAppService : ApplicationService, INotificationAppService
    {
        private readonly INotificationRepository _notificationRepository;

        public NotificationAppService(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        public async Task<PagedResultDto<NotificationDto>> GetListAsync(Guid reporterId, PagedAndSortedResultRequestDto input)
        {
            var queryable = (await _notificationRepository.GetQueryableAsync())
                .Where(n => n.ReporterId == reporterId);

            var totalCount = queryable.Count();
            var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "CreationTime desc" : input.Sorting;

            var notifications = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
            );

            return new PagedResultDto<NotificationDto>(
                totalCount,
                ObjectMapper.Map<System.Collections.Generic.List<Notification>, System.Collections.Generic.List<NotificationDto>>(notifications)
            );
        }

        public async Task<NotificationDto> CreateAsync(CreateNotificationDto input)
        {
            var notification = new Notification(GuidGenerator.Create(), input.ReporterId, input.ReportId, input.Title, input.Message);
            await _notificationRepository.InsertAsync(notification);
            return ObjectMapper.Map<Notification, NotificationDto>(notification);
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            var notification = await _notificationRepository.GetAsync(id);
            notification.MarkAsRead();
            await _notificationRepository.UpdateAsync(notification);
        }
    }
}
