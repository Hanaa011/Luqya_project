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
    public interface ILostFoundDbContext : IEfCoreDbContext
    {
        DbSet<Category> Categories { get; }
        DbSet<Location> Locations { get; }
        DbSet<Reporter> Reporters { get; }
        DbSet<Report> Reports { get; }
        DbSet<Notification> Notifications { get; }
        DbSet<Match> Matches { get; }
        DbSet<ReportClaim> ReportClaims { get; }
        DbSet<Conversation> Conversations { get; }
    }
}
