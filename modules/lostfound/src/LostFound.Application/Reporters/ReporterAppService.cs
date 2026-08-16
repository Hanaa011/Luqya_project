using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Reporters.Dtos;

namespace LostFound.Reporters
{
    public class ReporterAppService : ApplicationService, IReporterAppService
    {
        private readonly IReporterRepository _reporterRepository;
        private readonly ReporterManager _reporterManager;

        public ReporterAppService(
            IReporterRepository reporterRepository,
            ReporterManager reporterManager)
        {
            _reporterRepository = reporterRepository;
            _reporterManager = reporterManager;
        }

        public async Task<ReporterDto> GetAsync(Guid id)
        {
            var reporter = await _reporterRepository.GetAsync(id);
            return ObjectMapper.Map<Reporter, ReporterDto>(reporter);
        }

        public async Task<PagedResultDto<ReporterDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var queryable = await _reporterRepository.GetQueryableAsync();
            var totalCount = queryable.Count();
            var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "CreationTime desc" : input.Sorting;

            var reporters = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
            );

            return new PagedResultDto<ReporterDto>(
                totalCount,
                ObjectMapper.Map<System.Collections.Generic.List<Reporter>, System.Collections.Generic.List<ReporterDto>>(reporters)
            );
        }

        public async Task<ReporterDto> CreateAsync(CreateReporterDto input)
        {
            var reporter = await _reporterManager.FindOrCreateForGuestAsync(
                input.Name,
                input.Phone,
                input.Email,
                input.PreferredContact
            );

            return ObjectMapper.Map<Reporter, ReporterDto>(reporter);
        }

        public async Task<ReporterDto> UpdateAsync(Guid id, UpdateReporterDto input)
        {
            var reporter = await _reporterRepository.GetAsync(id);

            await _reporterManager.UpdateContactInfoAsync(
                reporter,
                input.Name,
                input.Email,
                input.PreferredContact
            );

            await _reporterRepository.UpdateAsync(reporter);

            return ObjectMapper.Map<Reporter, ReporterDto>(reporter);
        }

        public async Task DeleteAsync(Guid id)
        {
            await _reporterRepository.DeleteAsync(id);
        }
    }
}
