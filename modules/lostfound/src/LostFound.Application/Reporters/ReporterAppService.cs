using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using LostFound.Matches;
using LostFound.Reports;
using LostFound.Reporters.Dtos;

namespace LostFound.Reporters
{
    public class ReporterAppService : ApplicationService, IReporterAppService
    {
        // Phase 4 Part 2: a caller may see a reporter's contact information
        // only if they are the authenticated owner of a report linked, via
        // an existing Match row (any status - Match.jsx's own Contact-link
        // condition never checks match.status, so this mirrors that exactly
        // rather than over-restricting to Accepted-only), to that
        // reporter's report. Same message for "not signed in" and "signed
        // in but unrelated" - both are simply "not authorized to view this",
        // and collapsing them avoids leaking which case applies.
        private const string ContactAuthorizationDeniedMessage =
            "You can only view contact information for a reporter linked to one of your own reports through an existing match.";

        private readonly IReporterRepository _reporterRepository;
        private readonly IReportRepository _reportRepository;
        private readonly IMatchRepository _matchRepository;
        private readonly IReportClaimRepository _reportClaimRepository;
        private readonly ReporterManager _reporterManager;

        public ReporterAppService(
            IReporterRepository reporterRepository,
            IReportRepository reportRepository,
            IMatchRepository matchRepository,
            IReportClaimRepository reportClaimRepository,
            ReporterManager reporterManager)
        {
            _reporterRepository = reporterRepository;
            _reportRepository = reportRepository;
            _matchRepository = matchRepository;
            _reportClaimRepository = reportClaimRepository;
            _reporterManager = reporterManager;
        }

        // Privacy fix: in-platform conversations (LostFound.Conversations)
        // are now the intended way to reach a reporter - this legacy
        // lookup's authorization (EnsureCanViewReporterAsync, unchanged)
        // still gates who reaches this method at all, but the raw
        // Phone/Email are now redacted from the returned DTO regardless of
        // who's asking, so the old contact-info leak is closed whether
        // this is called from the frontend or directly (Swagger/Postman/
        // any REST client). CreateAsync/UpdateAsync are deliberately NOT
        // touched - those return the caller's own just-submitted contact
        // info, not someone else's.
        public async Task<ReporterDto> GetAsync(Guid id)
        {
            await EnsureCanViewReporterAsync(id);

            var reporter = await _reporterRepository.GetAsync(id);
            return RedactContactInfo(ObjectMapper.Map<Reporter, ReporterDto>(reporter));
        }

        public async Task<PagedResultDto<ReporterDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var relatedReporterIds = await GetRelatedReporterIdsQueryableAsync();

            var queryable = (await _reporterRepository.GetQueryableAsync())
                .Where(r => relatedReporterIds.Contains(r.Id));

            var totalCount = await AsyncExecuter.CountAsync(queryable);
            var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "CreationTime desc" : input.Sorting;

            var reporters = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
            );

            var dtos = ObjectMapper.Map<System.Collections.Generic.List<Reporter>, System.Collections.Generic.List<ReporterDto>>(reporters);
            dtos.ForEach(dto => RedactContactInfo(dto));

            return new PagedResultDto<ReporterDto>(totalCount, dtos);
        }

        private static ReporterDto RedactContactInfo(ReporterDto dto)
        {
            dto.Phone = string.Empty;
            dto.Email = null;
            return dto;
        }

        // Every report id created by a reporter the current user is
        // entitled to contact: any report the user owns (CreatorId) that
        // has an existing Match (either side, any status) to a report
        // authored by that reporter - PLUS (Phase 4 Part 6, Task B) any
        // report the current user has directly claimed ("this is my item"
        // with no eligible own report of their own - LostFound.Domain.Matches.ReportClaim).
        // Both are the same kind of fact ("this authenticated user has a
        // confirmed, specific reason to contact this reporter") - a
        // ReportClaim just doesn't require a second, paired report to
        // record it. This is additive only: it never removes a Match-based
        // grant, and a report nobody has claimed or matched against still
        // grants nothing.
        private async Task<IQueryable<Guid>> GetRelatedReporterIdsQueryableAsync()
        {
            if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null)
            {
                return Enumerable.Empty<Guid>().AsQueryable();
            }

            var reportQueryable = await _reportRepository.GetQueryableAsync();
            var matchQueryable = await _matchRepository.GetQueryableAsync();
            var claimQueryable = await _reportClaimRepository.GetQueryableAsync();

            var myReportIds = reportQueryable
                .Where(r => r.CreatorId == CurrentUser.Id)
                .Select(r => r.Id);

            var relatedOtherReportIds = matchQueryable
                .Where(m => myReportIds.Contains(m.LostReportId) || myReportIds.Contains(m.FoundReportId))
                .Select(m => myReportIds.Contains(m.LostReportId) ? m.FoundReportId : m.LostReportId);

            // Phase 4 Part 8 (Task B): IsMine == true only - ReportClaim
            // now also represents "not my item" (a dismissal), which must
            // NEVER grant contact access. Without this filter, dismissing
            // a result as "not mine" would perversely unlock the same
            // reporter's contact info it just said had nothing to do with
            // the caller - a real security regression this filter exists
            // specifically to prevent.
            var directlyClaimedReportIds = claimQueryable
                .Where(c => c.ClaimantUserId == CurrentUser.Id && c.IsMine)
                .Select(c => c.ReportId);

            var accessibleReportIds = relatedOtherReportIds.Concat(directlyClaimedReportIds);

            return reportQueryable
                .Where(r => accessibleReportIds.Contains(r.Id))
                .Select(r => r.ReporterId)
                .Distinct();
        }

        private async Task EnsureCanViewReporterAsync(Guid reporterId)
        {
            var relatedReporterIds = await GetRelatedReporterIdsQueryableAsync();
            var isRelated = await AsyncExecuter.AnyAsync(relatedReporterIds.Where(rid => rid == reporterId));

            if (!isRelated)
            {
                throw new AbpAuthorizationException(ContactAuthorizationDeniedMessage);
            }
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

        public async Task<ConfirmReporterClaimResultDto> ConfirmClaimAsync(ConfirmReporterClaimDto input)
        {
            if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null)
            {
                throw new AbpAuthorizationException("You must be signed in to confirm this link.");
            }

            var reporter = await _reporterManager.ClaimGuestReportAsync(input.Token, CurrentUser.Id.Value);

            return new ConfirmReporterClaimResultDto { ReporterId = reporter.Id };
        }
    }
}
