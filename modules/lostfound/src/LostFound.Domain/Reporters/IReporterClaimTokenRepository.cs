using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Reporters
{
    public interface IReporterClaimTokenRepository : IRepository<ReporterClaimToken, Guid>
    {
        Task<ReporterClaimToken?> FindByTokenHashAsync(string tokenHash);

        // Idempotency check for issuance: a still-valid (unused, unexpired)
        // token for this reporter means a claim email is already out there -
        // see ReporterManager.IssueClaimTokenIfNeededAsync.
        Task<ReporterClaimToken?> FindValidForReporterAsync(Guid reporterId, DateTime utcNow);
    }
}
