using System.ComponentModel.DataAnnotations;
using LostFound.Locations;

namespace LostFound.Locations.Dtos
{
    public class CreateUpdateLocationDto
    {
        [Required]
        [StringLength(LocationConsts.MaxPlaceNameLength)]
        public string PlaceName { get; set; } = string.Empty;
    }
}
