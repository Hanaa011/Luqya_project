using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Matches
{
    public interface IReportClaimRepository : IRepository<ReportClaim, Guid>
    {
        // Idempotency: a user re-confirming "this is my item" (e.g. a
        // retried request, or landing back on this page again after the
        // Task A login round trip) must reuse the existing row rather than
        // accumulate duplicates for the same (report, claimant) pair -
        // same shape as IMatchRepository.FindByPairAsync.
        Task<ReportClaim?> FindAsync(Guid reportId, Guid claimantUserId);
    }
}
