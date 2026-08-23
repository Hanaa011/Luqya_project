using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using LostFound.AI.Core;
using LostFound.Matches.Dtos;
using LostFound.Reports;

namespace LostFound.Matches
{
    public class MatchAppService : ApplicationService, IMatchAppService
    {
        private readonly IMatchRepository _matchRepository;
        private readonly IReportRepository _reportRepository;
        private readonly MatchManager _matchManager;
        private readonly IEmbeddingEngine _embeddingEngine;
        private readonly IConfiguration _configuration;

        public MatchAppService(
            IMatchRepository matchRepository,
            IReportRepository reportRepository,
            MatchManager matchManager,
            IEmbeddingEngine embeddingEngine,
            IConfiguration configuration)
        {
            _matchRepository = matchRepository;
            _reportRepository = reportRepository;
            _matchManager = matchManager;
            _embeddingEngine = embeddingEngine;
            _configuration = configuration;
        }

        // Mirrors ReportMatchingBackgroundJob's own threshold/provider-name
        // resolution exactly, so a manual recompute and the automatic
        // background-job path can never silently disagree on either value.
        public async Task<int> RecomputeMatchesAsync(Guid reportId)
        {
            var threshold = _configuration.GetValue<double?>("LostFound:AI:MatchThreshold") ?? 75;

            return await _matchManager.FindAndCreateMatchesAsync(reportId, threshold, _embeddingEngine.EngineName);
        }

        public async Task<PagedResultDto<MatchDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var queryable = await _matchRepository.GetQueryableAsync();
            var totalCount = queryable.Count();
            var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "CreationTime desc" : input.Sorting;

            var matches = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
            );

            return new PagedResultDto<MatchDto>(totalCount, matches.Select(MapToDto).ToList());
        }

        public async Task<MatchDto> AcceptAsync(Guid id)
        {
            var match = await _matchRepository.GetAsync(id);
            match.Accept();
            await _matchRepository.UpdateAsync(match);

            await _matchManager.NotifyMatchDecisionAsync(match, accepted: true);

            return MapToDto(match);
        }

        public async Task<MatchDto> RejectAsync(Guid id)
        {
            var match = await _matchRepository.GetAsync(id);
            match.Reject();
            await _matchRepository.UpdateAsync(match);

            await _matchManager.NotifyMatchDecisionAsync(match, accepted: false);

            return MapToDto(match);
        }

        // Phase 4 Part 3 (Task A.2/A.3): "This is my item" / "Not my item"
        // from Smart Search. Ownership of OwnReportId is verified here
        // (Application layer, where CurrentUser is naturally available) -
        // MatchManager.GetOrCreateMatchForClaimAsync itself stays a plain
        // domain service with no auth concerns, consistent with the rest of
        // this class's methods.
        //
        // Phase 4 Part 6 (Task B): OwnReportId is now optional. When the
        // caller genuinely owns no eligible report, confirming "this is my
        // item" no longer requires creating one first - see the null
        // branch below, which records a narrower ReportClaim instead of a
        // full, two-sided Match and still grants immediate contact access.
        public async Task<ClaimResultDto> ClaimAsync(ClaimMatchDto input)
        {
            if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null)
            {
                throw new AbpAuthorizationException("You must be signed in to claim a search result.");
            }

            if (!input.OwnReportId.HasValue)
            {
                // "Not my item" with no own report has nothing to scope the
                // dismissal to (the search-time exclusion filter - Phase 4
                // Part 3/4 - works by cross-referencing the caller's own
                // reports' rejected matches) - the frontend's own picker
                // already never reaches this endpoint in that case; this is
                // a defensive guard, not a real path.
                if (!input.IsMine)
                {
                    throw new UserFriendlyException(
                        "Dismissing a search result requires one of your own reports to link the dismissal to.");
                }

                await _matchManager.GetOrCreateClaimWithoutOwnReportAsync(
                    input.SearchResultReportId, CurrentUser.Id.Value, input.ObservedScorePercentage);

                return new ClaimResultDto { Match = null, ContactAccessGranted = true };
            }

            var ownReport = await _reportRepository.GetAsync(input.OwnReportId.Value);
            if (ownReport.CreatorId != CurrentUser.Id)
            {
                throw new AbpAuthorizationException("You can only claim a search result against a report you own.");
            }

            var match = await _matchManager.GetOrCreateMatchForClaimAsync(
                input.SearchResultReportId, input.OwnReportId.Value, input.ObservedScorePercentage);

            if (input.IsMine)
            {
                // Decision #2: reuses AcceptAsync exactly as-is (including
                // its existing "both parties notified" side effect) so
                // Contact access unlocks immediately, with no dependency on
                // the other party - AcceptAsync already behaves this way
                // today (Match.jsx's Contact link never checks match.status),
                // so no adjustment to Accept's own semantics was needed.
                var accepted = await AcceptAsync(match.Id);
                return new ClaimResultDto { Match = accepted, ContactAccessGranted = true };
            }

            // "Not my item": persisted as MatchStatus.Rejected on the same
            // row (decision #3 - sufficient on its own, see the report's
            // discussion of search-time exclusion), but deliberately NOT a
            // call to the existing RejectAsync - that method's
            // NotifyMatchDecisionAsync side effect is meant for a match
            // both parties already knew was under review (an AI suggestion
            // being turned down); a candidate the searching user silently
            // decided isn't theirs was never surfaced to the other party at
            // all, so notifying them "someone rejected you" would be noise
            // about an interaction they were never part of.
            match.Reject();
            await _matchRepository.UpdateAsync(match);
            return new ClaimResultDto { Match = MapToDto(match), ContactAccessGranted = false };
        }

        private static MatchDto MapToDto(Match match)
        {
            return new MatchDto
            {
                Id = match.Id,
                CreationTime = match.CreationTime,
                LastModificationTime = match.LastModificationTime,
                LostReportId = match.LostReportId,
                FoundReportId = match.FoundReportId,
                SimilarityScore = match.SimilarityScore,
                MatchReason = match.MatchReason,
                Status = match.Status,
                IsAutoGenerated = match.IsAutoGenerated
            };
        }
    }
}
