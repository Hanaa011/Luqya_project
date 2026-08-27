using System;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using LostFound.Categories;
using LostFound.Locations;
using LostFound.Reporters;
using LostFound.Reports;
using LostFound.Notifications;
using LostFound.Matches;
using LostFound.Conversations;

namespace LostFound.EntityFrameworkCore
{
    [ConnectionStringName("Default")]
    public class LostFoundDbContext : AbpDbContext<LostFoundDbContext>, ILostFoundDbContext
    {
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Location> Locations { get; set; } = null!;
        public DbSet<Reporter> Reporters { get; set; } = null!;
        public DbSet<Report> Reports { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<Match> Matches { get; set; } = null!;
        public DbSet<ReportClaim> ReportClaims { get; set; } = null!;
        public DbSet<Conversation> Conversations { get; set; } = null!;

        public LostFoundDbContext(DbContextOptions<LostFoundDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Standalone use of this module's own DbContext (e.g. running
            // its own migrations) needs this call too, in addition to the
            // Host calling builder.ConfigureLostFound() from its own
            // DbContext.
            builder.ConfigureLostFound();
        }
    }
}
