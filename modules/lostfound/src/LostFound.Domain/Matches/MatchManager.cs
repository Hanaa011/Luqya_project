using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using LostFound.Reports;
using LostFound.Matching;
using LostFound.Notifications;

namespace LostFound.Matches
{
    public class MatchManager : DomainService
    {
        private readonly IMatchRepository _matchRepository;
        private readonly IReportClaimRepository _reportClaimRepository;
        private readonly IReportRepository _reportRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IMatchRankingService _matchRankingService;
        private readonly ILogger<MatchManager> _logger;

        public MatchManager(
            IMatchRepository matchRepository,
            IReportClaimRepository reportClaimRepository,
            IReportRepository reportRepository,
            INotificationRepository notificationRepository,
            IMatchRankingService matchRankingService,
            ILogger<MatchManager> logger)
        {
            _matchRepository = matchRepository;
            _reportClaimRepository = reportClaimRepository;
            _reportRepository = reportRepository;
            _notificationRepository = notificationRepository;
            _matchRankingService = matchRankingService;
            _logger = logger;
        }

        // Called by the background job AFTER the report's embedding has
        // already been generated and saved.
        //
        // Architectural note (Arabic-E2E-Matching-Accuracy-Validation
        // follow-up): text scoring now flows through IMatchRankingService,
        // the SAME ranking pipeline (query understanding -> hybrid
        // retrieval -> feature scoring -> object-type/category/brand/color
        // metadata penalties -> confidence calibration) that
        // AiSearchAppService uses for real user searches - see
        // IMatchRankingService's own remarks for why this seam exists and
        // what it replaced (a separate, ad-hoc raw-cosine-similarity
        // calculation that could - and, with real evidence, did - reach a
        // different conclusion than Search for the exact same pair). This
        // is the single source of truth for text-relevance now; it is not
        // duplicated here.
        //
        // Category plays no role in CANDIDATE SELECTION (still just
        // "opposite type, Open, has an embedding" - see
        // IReportRepository.GetMatchCandidatesAsync) - only in scoring,
        // exactly as Search already treats it.
        //
        // Image similarity is blended in (60/40 with the unified text
        // score) when BOTH reports have an image embedding - this remains
        // a MatchManager-specific addition on top of the unified score,
        // not a duplicate of anything Search does, because
        // ISemanticSearchOrchestrator is text-only by design (see its own
        // remarks) and unifying image ranking too would be a materially
        // larger change than this fix's scope.
        public async Task<int> FindAndCreateMatchesAsync(
            Guid reportId,
            double thresholdPercentage,
            string providerNameForExplanation)
        {
            var report = await _reportRepository.GetAsync(reportId);

            var textEmbedding = report.GetEmbeddingVector();
            if (textEmbedding == null)
            {
                return 0;
            }

            var oppositeType = report.Type == ReportType.Lost ? ReportType.Found : ReportType.Lost;

            var candidates = await _reportRepository.GetMatchCandidatesAsync(report.Id, oppositeType);

            // Resilience fallback, not the normal path: if the shared
            // ranking pipeline itself is unavailable this run (a transient
            // embedding/LLM/DB hiccup somewhere inside query understanding
            // or retrieval), matching degrades to the old raw-embedding
            // comparison rather than silently producing zero matches for
            // every candidate - logged clearly so this is never a silent
            // degradation.
            IReadOnlyDictionary<Guid, double>? unifiedScores = null;
            try
            {
                unifiedScores = await _matchRankingService.RankCandidatesAsync(report);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "MatchManager: unified ranking pipeline failed for ReportId={ReportId}; falling back to raw embedding " +
                    "similarity for this run only.",
                    reportId);
            }

            var createdCount = 0;

            foreach (var candidate in candidates)
            {
                double score;

                if (unifiedScores != null)
                {
                    if (!unifiedScores.TryGetValue(candidate.Id, out score))
                    {
                        // The shared ranking pipeline considered this
                        // candidate (it is text-searchable, opposite type)
                        // and found no meaningful signal for it at all -
                        // exactly what a real search for `report`'s own
                        // text would show: this candidate simply would not
                        // appear. This IS the fix - Search and Matching now
                        // reach the same decision for the same pair, so a
                        // report the ranking pipeline would never surface
                        // is not scored via a different algorithm here.
                        continue;
                    }
                }
                else
                {
                    score = CosineSimilarityCalculator.CalculatePercentage(textEmbedding, candidate.GetEmbeddingVector());
                }

                var imageEmbedding = report.GetImageEmbeddingVector();
                var candidateImageEmbedding = candidate.GetImageEmbeddingVector();

                if (imageEmbedding != null && candidateImageEmbedding != null)
                {
                    var imageScore = CosineSimilarityCalculator.CalculatePercentage(imageEmbedding, candidateImageEmbedding);
                    score = (score * 0.6) + (imageScore * 0.4);
                }

                if (score < thresholdPercentage)
                {
                    continue;
                }

                var lostReportId = report.Type == ReportType.Lost ? report.Id : candidate.Id;
                var foundReportId = report.Type == ReportType.Found ? report.Id : candidate.Id;

                if (await _matchRepository.ExistsForPairAsync(lostReportId, foundReportId))
                {
                    continue;
                }

                var explanation = MatchExplanationBuilder.Build(score, providerNameForExplanation);

                var match = new Match(
                    GuidGenerator.Create(),
                    lostReportId,
                    foundReportId,
                    (decimal)score,
                    explanation,
                    MatchStatus.Pending,
                    isAutoGenerated: true
                );

                await _matchRepository.InsertAsync(match);

                await NotifyBothReportersAsync(
                    match,
                    lostReportId == report.Id ? report : candidate,
                    foundReportId == report.Id ? report : candidate,
                    "Possible match found",
                    $"We found a possible match with {score:0.0}% confidence. Please review it."
                );

                createdCount++;
            }

            return createdCount;
        }

        // Phase 4 Part 3 (Task A.1): the narrow "claim a specific search
        // result" capability - deliberately NOT a variant of
        // FindAndCreateMatchesAsync above. That method scores and
        // potentially creates matches for a report's ENTIRE opposite-type
        // candidate pool; this one touches exactly the one pair the caller
        // named, using the score they were actually shown (decision #1),
        // never recomputing it. Reuses the same ExistsForPairAsync-style
        // pair identity (now via FindByPairAsync, so an existing row - of
        // any origin, auto-generated or not - is reused rather than
        // duplicated) and the same Notification side effect shape as
        // FindAndCreateMatchesAsync, without re-running any scoring.
        public async Task<Match> GetOrCreateMatchForClaimAsync(
            Guid searchResultReportId,
            Guid ownReportId,
            double observedScorePercentage)
        {
            var searchResultReport = await _reportRepository.GetAsync(searchResultReportId);
            var ownReport = await _reportRepository.GetAsync(ownReportId);

            if (searchResultReport.Type == ownReport.Type)
            {
                throw new UserFriendlyException(
                    "A claim must link a Lost report to a Found report - these two reports are the same type.");
            }

            var lostReportId = ownReport.Type == ReportType.Lost ? ownReport.Id : searchResultReport.Id;
            var foundReportId = ownReport.Type == ReportType.Found ? ownReport.Id : searchResultReport.Id;

            var existing = await _matchRepository.FindByPairAsync(lostReportId, foundReportId);
            if (existing != null)
            {
                return existing;
            }

            var match = new Match(
                GuidGenerator.Create(),
                lostReportId,
                foundReportId,
                (decimal)observedScorePercentage,
                MatchExplanationBuilder.BuildForUserClaim(observedScorePercentage),
                MatchStatus.Pending,
                isAutoGenerated: false
            );

            // autoSave: true - MatchAppService.ClaimAsync immediately follows
            // this call with AcceptAsync(match.Id)/its own RejectAsync-style
            // update, each of which re-reads the Match by Id through its own
            // repository call. Without forcing the insert to flush here,
            // that read can race the still-pending insert within the same
            // unit of work and throw EntityNotFoundException - confirmed
            // live during this task's own verification, not a hypothetical.
            await _matchRepository.InsertAsync(match, autoSave: true);

            return match;
        }

        // Phase 4 Part 6 (Task B), extended by Phase 4 Part 8 (Task B): a
        // caller's recorded disposition toward one specific report,
        // independent of any own report - originally "this is my item"
        // only, now also "not my item" (see ReportClaim.IsMine).
        //
        // IsMine=true grants the same immediate contact access a real
        // claim would (see ReporterAppService.GetRelatedReporterIdsQueryableAsync,
        // which this record extends, filtered to IsMine=true only),
        // scoped to exactly this (claimant, report) pair - never a
        // general loosening of the Phase 4 Part 2 contact rule, and never
        // visible in any "both parties" Dashboard view, since there is no
        // second report to show it on. It also sends a one-sided
        // Notification to the claimed report's own reporter - there's no
        // second, paired report here to notify "both parties" the way
        // NotifyBothReportersAsync does for a real Match, but the report's
        // own reporter can still be told, honestly, that someone claims
        // this is theirs.
        //
        // IsMine=false (Phase 4 Part 8) grants no contact access and
        // sends no notification - a silent dismissal, matching Phase 4
        // Part 3's original decision that notifying an uninvolved
        // stranger "someone dismissed you" would be noise about an
        // interaction they were never part of.
        //
        // Idempotent, and now update-in-place: at most one row exists per
        // (reportId, claimantUserId) - re-confirming the same disposition
        // does nothing new; recording the OPPOSITE disposition (e.g. the
        // user first said "not mine", later decides it actually is)
        // updates the existing row rather than accumulating a second one,
        // since a user's disposition toward a given report is singular.
        // isNewClaim distinguishes a genuinely first-time (report, claimant)
        // pair from a repeat call that just re-confirms the same
        // disposition - MatchAppService uses it to decide whether a guest
        // contact-request email needs sending (never resend for a repeat
        // click - see ClaimResultDto.AlreadyRequested) and to know it must
        // NOT resend for an existing-but-changed disposition either
        // (isMine flipped from false to true), which reuses the existing
        // row rather than counting as "new".
        public async Task<(ReportClaim Claim, bool IsNewClaim)> GetOrCreateReportClaimAsync(
            Guid reportId, Guid claimantUserId, bool isMine, double observedScorePercentage)
        {
            var claimedReport = await _reportRepository.GetAsync(reportId);

            var existing = await _reportClaimRepository.FindAsync(reportId, claimantUserId);
            if (existing != null)
            {
                if (existing.IsMine == isMine)
                {
                    return (existing, false);
                }

                existing.UpdateDisposition(isMine, (decimal)observedScorePercentage);
                await _reportClaimRepository.UpdateAsync(existing, autoSave: true);
                return (existing, false);
            }

            var claim = new ReportClaim(GuidGenerator.Create(), reportId, claimantUserId, isMine, (decimal)observedScorePercentage);
            await _reportClaimRepository.InsertAsync(claim, autoSave: true);

            if (isMine)
            {
                await _notificationRepository.InsertAsync(
                    new Notification(
                        GuidGenerator.Create(),
                        claimedReport.ReporterId,
                        claimedReport.Id,
                        "Someone claimed your item",
                        "A user confirmed this report matches something they lost or found and can now see your contact details."
                    )
                );
            }

            return (claim, true);
        }

        public async Task NotifyMatchDecisionAsync(Match match, bool accepted)
        {
            var lostReport = await _reportRepository.GetAsync(match.LostReportId);
            var foundReport = await _reportRepository.GetAsync(match.FoundReportId);

            var title = accepted ? "Match accepted" : "Match rejected";
            var message = accepted
                ? "Your match was confirmed by the other party."
                : "Your match was rejected by the other party.";

            await NotifyBothReportersAsync(match, lostReport, foundReport, title, message);
        }

        private async Task NotifyBothReportersAsync(Match match, Report lostReport, Report foundReport, string title, string message)
        {
            await _notificationRepository.InsertAsync(
                new Notification(GuidGenerator.Create(), lostReport.ReporterId, lostReport.Id, title, message)
            );

            await _notificationRepository.InsertAsync(
                new Notification(GuidGenerator.Create(), foundReport.ReporterId, foundReport.Id, title, message)
            );
        }
    }
}
