using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using LostFound.Categories.Dtos;

namespace LostFound.Categories
{
    // Manual/admin CRUD - Categories are normally created automatically by
    // CategoryManager.FindOrCreateByNameAsync during AI classification.
    public class CategoryAppService : ApplicationService, ICategoryAppService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryAppService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<CategoryDto> GetAsync(Guid id)
        {
            var category = await _categoryRepository.GetAsync(id);
            return ObjectMapper.Map<Category, CategoryDto>(category);
        }

        public async Task<PagedResultDto<CategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var queryable = await _categoryRepository.GetQueryableAsync();
            var totalCount = queryable.Count();
            var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "Name" : input.Sorting;

            var categories = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
            );

            return new PagedResultDto<CategoryDto>(
                totalCount,
                ObjectMapper.Map<System.Collections.Generic.List<Category>, System.Collections.Generic.List<CategoryDto>>(categories)
            );
        }

        public async Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input)
        {
            var category = new Category(GuidGenerator.Create(), input.Name, input.Icon);
            await _categoryRepository.InsertAsync(category);
            return ObjectMapper.Map<Category, CategoryDto>(category);
        }

        public async Task<CategoryDto> UpdateAsync(Guid id, CreateUpdateCategoryDto input)
        {
            var category = await _categoryRepository.GetAsync(id);
            category.SetName(input.Name);
            category.SetIcon(input.Icon);
            await _categoryRepository.UpdateAsync(category);
            return ObjectMapper.Map<Category, CategoryDto>(category);
        }

        public async Task DeleteAsync(Guid id)
        {
            await _categoryRepository.DeleteAsync(id);
        }
    }
}
