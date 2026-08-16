using System.ComponentModel.DataAnnotations;
using LostFound.Categories;

namespace LostFound.Categories.Dtos
{
    public class CreateUpdateCategoryDto
    {
        [Required]
        [StringLength(CategoryConsts.MaxNameLength)]
        public string Name { get; set; } = string.Empty;

        [StringLength(CategoryConsts.MaxIconLength)]
        public string? Icon { get; set; }
    }
}
