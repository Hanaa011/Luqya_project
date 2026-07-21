using System;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Locations
{
    public interface ILocationRepository : IRepository<Location, Guid>
    {
    }
}
