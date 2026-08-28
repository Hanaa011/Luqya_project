using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Reporters.Dtos;

namespace LostFound.Reporters
{
    public interface IReporterAppService : IApplicationService
    {
        Task<ReporterDto> GetAsync(Guid id);

        Task<PagedResultDto<ReporterDto>> GetListAsync(PagedAndSortedResultRequestDto input);

        Task<ReporterDto> CreateAsync(CreateReporterDto input);

        Task<ReporterDto> UpdateAsync(Guid id, UpdateReporterDto input);

        Task DeleteAsync(Guid id);

        // Redeems a guest-report claim-verification link (sent by
        // ConversationAppService.OpenAsync) for the current, now-
        // authenticated caller.
        Task<ConfirmReporterClaimResultDto> ConfirmClaimAsync(ConfirmReporterClaimDto input);
    }
}
