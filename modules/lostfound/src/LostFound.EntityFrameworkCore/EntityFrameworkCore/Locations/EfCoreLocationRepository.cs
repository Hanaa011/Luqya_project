using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.EntityFrameworkCore;

namespace LostFound.Locations
{
    public class EfCoreLocationRepository :
        EfCoreRepository<LostFoundDbContext, Location, Guid>,
        ILocationRepository
    {
        public EfCoreLocationRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
    }
}
