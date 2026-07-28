using System;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Notifications
{
    public interface INotificationRepository : IRepository<Notification, Guid>
    {
    }
}
