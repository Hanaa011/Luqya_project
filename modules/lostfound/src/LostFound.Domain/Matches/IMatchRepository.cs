using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Matches
{
    public interface IMatchRepository : IRepository<Match, Guid>
    {
        Task<bool> ExistsForPairAsync(Guid lostReportId, Guid foundReportId);

        // Phase 4 Part 3: same pair-identity as ExistsForPairAsync, but
        // returns the row itself so a caller (MatchManager.GetOrCreateMatchForClaimAsync)
        // can reuse an existing Match instead of just detecting one.
        Task<Match?> FindByPairAsync(Guid lostReportId, Guid foundReportId);
    }
}
