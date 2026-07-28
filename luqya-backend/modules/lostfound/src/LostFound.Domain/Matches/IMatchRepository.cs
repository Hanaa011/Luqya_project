using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Matches
{
    public interface IMatchRepository : IRepository<Match, Guid>
    {
        Task<bool> ExistsForPairAsync(Guid lostReportId, Guid foundReportId);
    }
}
