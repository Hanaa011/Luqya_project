using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using LostFound.Categories;
using LostFound.Categories.Dtos;
using LostFound.Locations;
using LostFound.Locations.Dtos;
using LostFound.Reporters;
using LostFound.Reporters.Dtos;
using LostFound.Notifications;
using LostFound.Notifications.Dtos;
using LostFound.Matches;
using LostFound.Matches.Dtos;

namespace LostFound
{
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
    public partial class LostFoundCategoryToCategoryDtoMapper : MapperBase<Category, CategoryDto>
    {
        public override partial CategoryDto Map(Category source);
        public override partial void Map(Category source, CategoryDto destination);
    }

    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
    public partial class LostFoundLocationToLocationDtoMapper : MapperBase<Location, LocationDto>
    {
        public override partial LocationDto Map(Location source);
        public override partial void Map(Location source, LocationDto destination);
    }

    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
    public partial class LostFoundReporterToReporterDtoMapper : MapperBase<Reporter, ReporterDto>
    {
        public override partial ReporterDto Map(Reporter source);
        public override partial void Map(Reporter source, ReporterDto destination);
    }

    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
    public partial class LostFoundNotificationToNotificationDtoMapper : MapperBase<Notification, NotificationDto>
    {
        public override partial NotificationDto Map(Notification source);
        public override partial void Map(Notification source, NotificationDto destination);
    }

    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
    public partial class LostFoundMatchToMatchDtoMapper : MapperBase<Match, MatchDto>
    {
        public override partial MatchDto Map(Match source);
        public override partial void Map(Match source, MatchDto destination);
    }
}
