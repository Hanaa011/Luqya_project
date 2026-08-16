using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Locations.Dtos;

namespace LostFound.Locations
{
    public class LocationAppService : ApplicationService, ILocationAppService
    {
        private readonly ILocationRepository _locationRepository;

        public LocationAppService(ILocationRepository locationRepository)
        {
            _locationRepository = locationRepository;
        }

        public async Task<LocationDto> GetAsync(Guid id)
        {
            var location = await _locationRepository.GetAsync(id);
            return ObjectMapper.Map<Location, LocationDto>(location);
        }

        public async Task<PagedResultDto<LocationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var queryable = await _locationRepository.GetQueryableAsync();
            var totalCount = queryable.Count();
            var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "PlaceName" : input.Sorting;

            var locations = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
            );

            return new PagedResultDto<LocationDto>(
                totalCount,
                ObjectMapper.Map<System.Collections.Generic.List<Location>, System.Collections.Generic.List<LocationDto>>(locations)
            );
        }

        public async Task<LocationDto> CreateAsync(CreateUpdateLocationDto input)
        {
            var location = new Location(GuidGenerator.Create(), input.PlaceName);
            await _locationRepository.InsertAsync(location);
            return ObjectMapper.Map<Location, LocationDto>(location);
        }

        public async Task<LocationDto> UpdateAsync(Guid id, CreateUpdateLocationDto input)
        {
            var location = await _locationRepository.GetAsync(id);
            location.SetPlaceName(input.PlaceName);
            await _locationRepository.UpdateAsync(location);
            return ObjectMapper.Map<Location, LocationDto>(location);
        }

        public async Task DeleteAsync(Guid id)
        {
            await _locationRepository.DeleteAsync(id);
        }
    }
}
