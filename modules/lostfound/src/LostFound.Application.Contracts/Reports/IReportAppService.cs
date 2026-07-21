using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Reports.Dtos;

namespace LostFound.Reports
{
    // CRUD ONLY - AI search lives in IAiSearchAppService, AI match review
    // lives in IMatchAppService.
    public interface IReportAppService : IApplicationService
    {
        Task<ReportDto> GetAsync(Guid id);
        Task<PagedResultDto<ReportDto>> GetListAsync(GetReportListDto input);
        Task<ReportDto> CreateAsync(CreateReportDto input);
        Task<ReportDto> UpdateAsync(Guid id, UpdateReportDto input);
        Task DeleteAsync(Guid id);
    }
}
