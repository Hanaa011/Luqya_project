using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using LostFound.Analytics.Dtos;

namespace LostFound.Analytics
{
    // Reports/dashboards read AI-GENERATED metadata (Category, ObjectType,
    // Color, Brand) instead of manually-selected values.
    public interface IReportAnalyticsAppService : IApplicationService
    {
        Task<ReportAnalyticsDto> GetAsync(int topN = 10);
    }
}
