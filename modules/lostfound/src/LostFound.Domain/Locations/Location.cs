using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Locations
{
    public class Location : FullAuditedAggregateRoot<Guid>
    {
        public virtual string PlaceName { get; private set; }

        protected Location()
        {
            PlaceName = string.Empty;
        }

        public Location(Guid id, string placeName) : base(id)
        {
            PlaceName = string.Empty;
            SetPlaceName(placeName);
        }

        public Location SetPlaceName(string placeName)
        {
            PlaceName = Check.NotNullOrWhiteSpace(placeName, nameof(placeName), maxLength: LocationConsts.MaxPlaceNameLength);
            return this;
        }
    }
}
