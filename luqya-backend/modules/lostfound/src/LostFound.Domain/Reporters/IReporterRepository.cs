using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Reporters
{
    public interface IReporterRepository : IRepository<Reporter, Guid>
    {
        Task<Reporter?> FindByIdentityUserIdAsync(Guid identityUserId);

        Task<Reporter?> FindByPhoneAsync(string phone);
    }
}
