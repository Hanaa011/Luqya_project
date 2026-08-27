using Microsoft.EntityFrameworkCore;
using LostFound.Categories;
using LostFound.Locations;
using LostFound.Reporters;
using LostFound.Reports;
using LostFound.Notifications;
using LostFound.Matches;
using LostFound.Conversations;

namespace LostFound.EntityFrameworkCore
{
    public static class LostFoundDbContextModelCreatingExtensions
    {
        public static void ConfigureLostFound(this ModelBuilder builder)
        {
            // Order matters: entities with FKs must be configured after the
            // entities they reference.
            builder.ConfigureCategory();
            builder.ConfigureLocation();
            builder.ConfigureReporter();
            builder.ConfigureReport();
            builder.ConfigureNotification();
            builder.ConfigureMatch();
            builder.ConfigureReportClaim();
            builder.ConfigureConversation();
        }
    }
}
