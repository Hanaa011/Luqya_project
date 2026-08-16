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

        // Re-runs matching for an existing, already-classified/embedded
        // report without repeating classification or embedding generation -
        // e.g. after a ranking/matching algorithm change, so existing
        // reports pick up the new logic without the cost of a full
        // reclassification pass. Returns the number of new matches created
        // (existing matches for this report are left untouched, not
        // recomputed - ExistsForPairAsync in MatchManager already prevents
        // duplicates).
        Task<int> RecomputeMatchesAsync(Guid reportId);
    }
}
