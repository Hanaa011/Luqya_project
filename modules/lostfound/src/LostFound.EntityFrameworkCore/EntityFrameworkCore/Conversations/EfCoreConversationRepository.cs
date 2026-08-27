using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using LostFound.Conversations;

namespace LostFound.EntityFrameworkCore
{
    public class EfCoreConversationRepository :
        EfCoreRepository<LostFoundDbContext, Conversation, Guid>,
        IConversationRepository
    {
        public EfCoreConversationRepository(IDbContextProvider<LostFoundDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<Conversation?> FindByReportAndParticipantsAsync(Guid reportId, Guid participant1Id, Guid participant2Id)
        {
            var dbSet = await GetDbSetAsync();

            return await dbSet.FirstOrDefaultAsync(x =>
                x.ReportId == reportId &&
                x.Participant1Id == participant1Id &&
                x.Participant2Id == participant2Id);
        }

        public async Task<List<Conversation>> GetListForUserWithMessagesAsync(Guid userId)
        {
            var dbSet = await GetDbSetAsync();

            return await dbSet
                .Include(x => x.Messages)
                .Where(x => x.Participant1Id == userId || x.Participant2Id == userId)
                .ToListAsync();
        }

        public async Task<Conversation?> GetWithMessagesAsync(Guid id)
        {
            var dbSet = await GetDbSetAsync();

            return await dbSet.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
