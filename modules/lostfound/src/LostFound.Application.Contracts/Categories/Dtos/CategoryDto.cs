using System;
using Volo.Abp.Application.Dtos;

namespace LostFound.Categories.Dtos
{
    public class CategoryDto : AuditedEntityDto<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }
}
