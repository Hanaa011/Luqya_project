using System;
using System.Collections.Generic;
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

        // Phase 4 Part 3: the user-initiated counterpart to the above two -
        // claims (or dismisses) one specific Smart-Search result. Unlike
        // RecomputeMatchesAsync, this never scores or touches any other
        // candidate. Phase 4 Part 6 (Task B): ClaimMatchDto.OwnReportId is
        // optional for "this is my item" - see ClaimResultDto and
        // ClaimMatchDto for what changes when it's absent. Phase 4 Part 8
        // (Task B): "not my item" (IsMine=false) now always ignores
        // OwnReportId entirely - a dismissal never requires or depends on
        // any report the caller owns.
        Task<ClaimResultDto> ClaimAsync(ClaimMatchDto input);

        // Phase 4 Part 8 (Task B): report ids the current user has
        // recorded a "not my item" disposition toward - powers the
        // search-time exclusion filter (Phase 4 Part 3/4) for users with
        // no own reports to key the older Match-based exclusion off of.
        Task<List<Guid>> GetMyDismissedReportIdsAsync();
    }
}
