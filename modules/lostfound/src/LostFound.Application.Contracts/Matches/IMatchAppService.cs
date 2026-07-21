using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Matches.Dtos;

namespace LostFound.Matches
{
    // Review of AI-generated matches ONLY - never creates a Report and never
    // does semantic search itself (see IAiSearchAppService for that).
    public interface IMatchAppService : IApplicationService
    {
        Task<PagedResultDto<MatchDto>> GetListAsync(PagedAndSortedResultRequestDto input);
        Task<MatchDto> AcceptAsync(Guid id);
        Task<MatchDto> RejectAsync(Guid id);
    }
}
