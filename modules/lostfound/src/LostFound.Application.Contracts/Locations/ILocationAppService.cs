using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Locations.Dtos;

namespace LostFound.Locations
{
    public interface ILocationAppService : IApplicationService
    {
        Task<LocationDto> GetAsync(Guid id);
        Task<PagedResultDto<LocationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
        Task<LocationDto> CreateAsync(CreateUpdateLocationDto input);
        Task<LocationDto> UpdateAsync(Guid id, CreateUpdateLocationDto input);
        Task DeleteAsync(Guid id);
    }
}
