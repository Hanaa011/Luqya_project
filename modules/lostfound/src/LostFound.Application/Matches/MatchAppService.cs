using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Emailing;
using LostFound.AI.Core;
using LostFound.Matches.Dtos;
using LostFound.Reporters;
using LostFound.Reports;

namespace LostFound.Matches
{
    public class MatchAppService : ApplicationService, IMatchAppService
    {
        private readonly IMatchRepository _matchRepository;
        private readonly IReportRepository _reportRepository;
        private readonly IReportClaimRepository _reportClaimRepository;
        private readonly IReporterRepository _reporterRepository;
        private readonly ReporterManager _reporterManager;
        private readonly IEmailSender _emailSender;
        private readonly MatchManager _matchManager;
        private readonly IEmbeddingEngine _embeddingEngine;
        private readonly IConfiguration _configuration;

        public MatchAppService(
            IMatchRepository matchRepository,
            IReportRepository reportRepository,
            IReportClaimRepository reportClaimRepository,
            IReporterRepository reporterRepository,
            ReporterManager reporterManager,
            IEmailSender emailSender,
            MatchManager matchManager,
            IEmbeddingEngine embeddingEngine,
            IConfiguration configuration)
        {
            _matchRepository = matchRepository;
            _reportRepository = reportRepository;
            _reportClaimRepository = reportClaimRepository;
            _reporterRepository = reporterRepository;
            _reporterManager = reporterManager;
            _emailSender = emailSender;
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
                var (_, isNewClaim) = await _matchManager.GetOrCreateReportClaimAsync(
                    input.SearchResultReportId, CurrentUser.Id.Value, isMine: true, input.ObservedScorePercentage);

                // Guest contact-request email: gated on isNewClaim, which is
                // keyed by (ReportId, ClaimantUserId) - the SAME requester
                // clicking again on the SAME report never re-sends (this
                // branch is skipped entirely); a DIFFERENT requester, or the
                // SAME requester on a DIFFERENT report, is always a fresh
                // (report, claimant) pair and always gets their own email.
                // Scope note: this only covers the no-own-report path above
                // (this session's actual "This is my item" flow) - the
                // separate has-an-own-report -> real Match -> AcceptAsync
                // path below is untouched, per its own existing
                // NotifyMatchDecisionAsync notification flow.
                if (isNewClaim)
                {
                    await SendGuestContactRequestEmailIfNeededAsync(input.SearchResultReportId);
                }

                return new ClaimResultDto { Match = null, ContactAccessGranted = true, AlreadyRequested = !isNewClaim };
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

        // Only fires for a genuinely guest-owned report (reporter has no
        // linked account yet) with an email on file - a registered owner
        // already gets the existing in-app Notification above instead.
        // Issues its own fresh, independent claim token every time (see
        // ReporterManager.IssueClaimTokenForRequestAsync) rather than
        // reusing ConversationAppService's reporter-scoped one, precisely
        // so a second, different requester on the same still-unclaimed
        // reporter gets their own working link even while an earlier
        // requester's token is still valid. Best-effort: a delivery
        // failure must not break the claim itself.
        private async Task SendGuestContactRequestEmailIfNeededAsync(Guid reportId)
        {
            var report = await _reportRepository.GetAsync(reportId);
            var reporter = await _reporterRepository.GetAsync(report.ReporterId);

            if (reporter.IdentityUserId != null || string.IsNullOrWhiteSpace(reporter.Email))
            {
                return;
            }

            var rawToken = await _reporterManager.IssueClaimTokenForRequestAsync(reporter.Id);
            var claimUrl = $"{_configuration["App:AngularUrl"]?.TrimEnd('/')}/claim/{rawToken}";
            const string subject = "Someone wants to contact you about your Luqya report";

            using var message = new MailMessage { Subject = subject, Body = BuildContactRequestPlainText(claimUrl), IsBodyHtml = false };
            message.To.Add(reporter.Email);
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(BuildContactRequestHtml(claimUrl), null, "text/html"));

            try
            {
                await _emailSender.SendAsync(message, normalize: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to send guest contact-request email.");
            }
        }

        private static string BuildContactRequestPlainText(string claimUrl) =>
            "Someone on Luqya believes a report you submitted may be theirs, and would like to contact you about it. " +
            $"To let them message you, verify it's yours: {claimUrl}\n\n" +
            "This link works once and expires in 60 minutes. If you didn't submit a report on Luqya, " +
            "you can ignore this email.";

        // Same table-based, inline-styled layout as
        // ConversationAppService's claim email - kept as its own small,
        // self-contained copy rather than a shared helper, so this change
        // stays fully localized to MatchAppService.
        private static string BuildContactRequestHtml(string claimUrl)
        {
            var encodedUrl = WebUtility.HtmlEncode(claimUrl);

            return $$"""
                <!DOCTYPE html>
                <html lang="en" dir="ltr">
                <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
                <body style="margin:0;padding:0;background-color:#f4f4f2;font-family:Segoe UI,Helvetica,Arial,sans-serif;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f2;padding:24px 0;">
                    <tr><td align="center">
                      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="max-width:480px;width:100%;background-color:#ffffff;border-radius:16px;overflow:hidden;">
                        <tr><td style="background-color:#0d7a6f;padding:20px 32px;">
                          <span style="font-size:20px;font-weight:700;color:#ffffff;">Luqya</span>
                        </td></tr>
                        <tr><td style="padding:32px;">
                          <p style="margin:0 0 16px 0;font-size:15px;line-height:1.6;color:#1f2937;">
                            Someone on Luqya believes a report you submitted may belong to them, and would like to
                            contact you about it.
                          </p>
                          <p style="margin:0 0 24px 0;font-size:15px;line-height:1.6;color:#1f2937;">
                            Confirm the report is yours to start a private conversation with them right here on Luqya.
                          </p>
                          <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 auto 24px auto;">
                            <tr><td align="center" style="border-radius:12px;background-color:#0d7a6f;">
                              <a href="{{encodedUrl}}" style="display:inline-block;padding:14px 32px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;">
                                تأكيد البلاغ والتواصل
                              </a>
                            </td></tr>
                          </table>
                          <p style="margin:0 0 16px 0;font-size:13px;line-height:1.5;color:#6b7280;">
                            This link works once and expires in 60 minutes.
                          </p>
                          <hr style="border:none;border-top:1px solid #e5e7eb;margin:16px 0;">
                          <p style="margin:0;font-size:12px;line-height:1.5;color:#9ca3af;">
                            If you didn't submit a report on Luqya, you can safely ignore this email - no account or
                            report will be linked without confirming this link yourself.
                          </p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;
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
