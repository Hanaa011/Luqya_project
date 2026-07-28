using System;
using Volo.Abp.Application.Dtos;

namespace LostFound.Locations.Dtos
{
    public class LocationDto : AuditedEntityDto<Guid>
    {
        public string PlaceName { get; set; } = string.Empty;
    }
}
