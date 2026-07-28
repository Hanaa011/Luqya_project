using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Notifications
{
    public class EfCoreNotificationRepository :
        EfCoreRepository<LostFoundDbContext, Notification, Guid>,
        INotificationRepository
    {
        public EfCoreNotificationRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
    }
}
