using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Categories.Dtos;

namespace LostFound.Categories
{
    // Manual/admin CRUD only - normal Report creation resolves categories
    // automatically through CategoryManager instead.
    public interface ICategoryAppService : IApplicationService
    {
        Task<CategoryDto> GetAsync(Guid id);
        Task<PagedResultDto<CategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
        Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input);
        Task<CategoryDto> UpdateAsync(Guid id, CreateUpdateCategoryDto input);
        Task DeleteAsync(Guid id);
    }
}
