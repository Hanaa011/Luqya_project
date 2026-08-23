using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
        private readonly IReportClaimRepository _reportClaimRepository;
        private readonly MatchManager _matchManager;
        private readonly IEmbeddingEngine _embeddingEngine;
        private readonly IConfiguration _configuration;

        public MatchAppService(
            IMatchRepository matchRepository,
            IReportRepository reportRepository,
            IReportClaimRepository reportClaimRepository,
            MatchManager matchManager,
            IEmbeddingEngine embeddingEngine,
            IConfiguration configuration)
        {
            _matchRepository = matchRepository;
            _reportRepository = reportRepository;
            _reportClaimRepository = reportClaimRepository;
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
        // Phase 4 Part 8 (Task B): "Not my item" (IsMine=false) is handled
        // FIRST, unconditionally, regardless of whether OwnReportId is
        // supplied - it always records a lightweight ReportClaim
        // disposition now (see MatchManager.GetOrCreateReportClaimAsync),
        // never touches Match at all, and never requires the caller to own
        // any report. This retires the old design (Phase 4 Part 3) where a
        // dismissal against an owned report created/rejected a real,
        // two-sided Match - that coupling was architecturally backwards
        // (a dismissal has no real reason to involve any of the caller's
        // own reports) and is what forced the picker to appear for "not my
        // item" at all. OwnReportId is accepted but ignored for this
        // action - the frontend no longer sends one (see Match.jsx's
        // simple confirm/cancel UI), but an old/cached client sending one
        // is harmless, not an error.
        public async Task<ClaimResultDto> ClaimAsync(ClaimMatchDto input)
        {
            if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null)
            {
                throw new AbpAuthorizationException("You must be signed in to claim a search result.");
            }

            if (!input.IsMine)
            {
                await _matchManager.GetOrCreateReportClaimAsync(
                    input.SearchResultReportId, CurrentUser.Id.Value, isMine: false, input.ObservedScorePercentage);

                return new ClaimResultDto { Match = null, ContactAccessGranted = false };
            }

            // Phase 4 Part 6 (Task B): OwnReportId is optional for "this is
            // my item". When the caller genuinely owns no eligible report
            // (or explicitly picked "none of these", Phase 4 Part 7),
            // confirming no longer requires creating one first - see the
            // null branch below, which records a narrower ReportClaim
            // instead of a full, two-sided Match and still grants
            // immediate contact access.
            if (!input.OwnReportId.HasValue)
            {
                await _matchManager.GetOrCreateReportClaimAsync(
                    input.SearchResultReportId, CurrentUser.Id.Value, isMine: true, input.ObservedScorePercentage);

                return new ClaimResultDto { Match = null, ContactAccessGranted = true };
            }

            var ownReport = await _reportRepository.GetAsync(input.OwnReportId.Value);
            if (ownReport.CreatorId != CurrentUser.Id)
            {
                throw new AbpAuthorizationException("You can only claim a search result against a report you own.");
            }

            var match = await _matchManager.GetOrCreateMatchForClaimAsync(
                input.SearchResultReportId, input.OwnReportId.Value, input.ObservedScorePercentage);

            // Decision #2 (Phase 4 Part 3): reuses AcceptAsync exactly
            // as-is (including its existing "both parties notified" side
            // effect) so Contact access unlocks immediately, with no
            // dependency on the other party - AcceptAsync already behaves
            // this way today (Match.jsx's Contact link never checks
            // match.status), so no adjustment to Accept's own semantics
            // was needed.
            var accepted = await AcceptAsync(match.Id);
            return new ClaimResultDto { Match = accepted, ContactAccessGranted = true };
        }

        // Phase 4 Part 8 (Task B, point 4): the report ids the current
        // user has recorded a "not my item" disposition toward - used by
        // the frontend's search-time exclusion filter (Phase 4 Part 3/4)
        // so a dismissed result never resurfaces, now regardless of
        // whether the dismissing user owns any report of their own (the
        // original exclusion mechanism could only key off the user's own
        // reports' rejected Match rows, which no longer applies once a
        // dismissal can be recorded with zero reports owned). Read-only,
        // scoped to exactly the caller's own dispositions - not a general
        // ReportClaim listing endpoint.
        public async Task<List<Guid>> GetMyDismissedReportIdsAsync()
        {
            if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null)
            {
                return new List<Guid>();
            }

            var queryable = await _reportClaimRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                queryable
                    .Where(c => c.ClaimantUserId == CurrentUser.Id.Value && !c.IsMine)
                    .Select(c => c.ReportId)
            );
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
