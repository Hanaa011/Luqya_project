using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Conversations
{
    public interface IConversationRepository : IRepository<Conversation, Guid>
    {
        // participant1Id/participant2Id must already be normalized (smaller
        // Guid first) by the caller - see ConversationAppService.
        Task<Conversation?> FindByReportAndParticipantsAsync(Guid reportId, Guid participant1Id, Guid participant2Id);

        Task<List<Conversation>> GetListForUserWithMessagesAsync(Guid userId);

        Task<Conversation?> GetWithMessagesAsync(Guid id);
    }
}
