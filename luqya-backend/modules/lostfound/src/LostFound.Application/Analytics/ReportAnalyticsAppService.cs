using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using LostFound.Reports;
using LostFound.Categories;
using LostFound.Locations;
using LostFound.Analytics.Dtos;

namespace LostFound.Analytics
{
    public class ReportAnalyticsAppService : ApplicationService, IReportAnalyticsAppService
    {
        private readonly IReportRepository _reportRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ILocationRepository _locationRepository;

        public ReportAnalyticsAppService(
            IReportRepository reportRepository,
            ICategoryRepository categoryRepository,
            ILocationRepository locationRepository)
        {
            _reportRepository = reportRepository;
            _categoryRepository = categoryRepository;
            _locationRepository = locationRepository;
        }

        public async Task<ReportAnalyticsDto> GetAsync(int topN = 10)
        {
            var reports = await _reportRepository.GetListAsync();
            var categories = await _categoryRepository.GetListAsync();
            var locations = await _locationRepository.GetListAsync();

            var categoryNameById = categories.ToDictionary(c => c.Id, c => c.Name);
            var locationNameById = locations.ToDictionary(l => l.Id, l => l.PlaceName);

            return new ReportAnalyticsDto
            {
                TopCategories = reports
                    .Where(r => r.CategoryId.HasValue && categoryNameById.ContainsKey(r.CategoryId.Value))
                    .GroupBy(r => categoryNameById[r.CategoryId!.Value])
                    .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(topN).ToList(),

                TopObjectTypes = reports
                    .Where(r => !string.IsNullOrWhiteSpace(r.AiObjectType))
                    .GroupBy(r => r.AiObjectType!)
                    .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(topN).ToList(),

                TopColors = reports
                    .Where(r => !string.IsNullOrWhiteSpace(r.Color))
                    .GroupBy(r => r.Color!)
                    .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(topN).ToList(),

                TopLocations = reports
                    .Where(r => locationNameById.ContainsKey(r.LocationId))
                    .GroupBy(r => locationNameById[r.LocationId])
                    .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(topN).ToList(),

                TopBrands = reports
                    .Where(r => !string.IsNullOrWhiteSpace(r.AiBrand))
                    .GroupBy(r => r.AiBrand!)
                    .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(topN).ToList()
            };
        }
    }
}
