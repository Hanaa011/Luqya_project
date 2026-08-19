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
        private readonly ReporterManager _reporterManager;

        public ReporterAppService(
            IReporterRepository reporterRepository,
            IReportRepository reportRepository,
            IMatchRepository matchRepository,
            ReporterManager reporterManager)
        {
            _reporterRepository = reporterRepository;
            _reportRepository = reportRepository;
            _matchRepository = matchRepository;
            _reporterManager = reporterManager;
        }

        public async Task<ReporterDto> GetAsync(Guid id)
        {
            await EnsureCanViewReporterAsync(id);

            var reporter = await _reporterRepository.GetAsync(id);
            return ObjectMapper.Map<Reporter, ReporterDto>(reporter);
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

            return new PagedResultDto<ReporterDto>(
                totalCount,
                ObjectMapper.Map<System.Collections.Generic.List<Reporter>, System.Collections.Generic.List<ReporterDto>>(reporters)
            );
        }

        // Every report id created by a reporter the current user is
        // entitled to contact: any report the user owns (CreatorId) that
        // has an existing Match (either side, any status) to a report
        // authored by that reporter.
        private async Task<IQueryable<Guid>> GetRelatedReporterIdsQueryableAsync()
        {
            if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null)
            {
                return Enumerable.Empty<Guid>().AsQueryable();
            }

            var reportQueryable = await _reportRepository.GetQueryableAsync();
            var matchQueryable = await _matchRepository.GetQueryableAsync();

            var myReportIds = reportQueryable
                .Where(r => r.CreatorId == CurrentUser.Id)
                .Select(r => r.Id);

            var relatedOtherReportIds = matchQueryable
                .Where(m => myReportIds.Contains(m.LostReportId) || myReportIds.Contains(m.FoundReportId))
                .Select(m => myReportIds.Contains(m.LostReportId) ? m.FoundReportId : m.LostReportId);

            return reportQueryable
                .Where(r => relatedOtherReportIds.Contains(r.Id))
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
    }
}
