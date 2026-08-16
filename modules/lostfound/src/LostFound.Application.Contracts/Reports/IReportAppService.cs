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

        // PHASE-VALIDATION-08: the image-based classification path
        // (ReportMatchingBackgroundJob reads report.ImagePath from
        // IBlobContainer<ReportImageContainer> - see that class) had no
        // way to actually populate ImagePath through the API at all until
        // now; this is the minimal upload endpoint that closes that gap so
        // image-only/image+text reports can be created and validated
        // through the real API, not just simulated. Returns the blob name
        // to pass as CreateReportDto.ImagePath / UpdateReportDto.ImagePath.
        Task<string> UploadImageAsync(byte[] imageBytes);
    }
}
