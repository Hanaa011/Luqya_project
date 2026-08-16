using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace LostFound.Categories
{
    // Resolves the category NAME the AI returned (it has no idea about our
    // Guid ids) into a real Category - reusing an existing one
    // case-insensitively, or creating a new one on the fly. Categories are
    // purely internal metadata now (analytics/reporting), never a required
    // user input.
    public class CategoryManager : DomainService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryManager(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<Category> FindOrCreateByNameAsync(string name)
        {
            Check.NotNullOrWhiteSpace(name, nameof(name));

            var normalized = name.Trim();

            var existing = await _categoryRepository.FindByNameAsync(normalized);
            if (existing != null)
            {
                return existing;
            }

            var category = new Category(GuidGenerator.Create(), normalized);
            await _categoryRepository.InsertAsync(category);
            return category;
        }
    }
}
