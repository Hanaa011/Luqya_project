using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using LostFound.Conversations.Dtos;

namespace LostFound.Conversations
{
    // Private in-platform messaging - replaces the old direct phone/email
    // exposure flow (Contact page). Every method verifies the current user
    // is a participant server-side; never relies on frontend checks alone.
    public interface IConversationAppService : IApplicationService
    {
        // "This is my item" entry point - creates or reuses the one
        // conversation for (reportId, current user, report owner). Named
        // OpenAsync rather than GetOrCreate*/Ensure* deliberately: an ABP
        // conventional controller would infer HttpGet for a "Get*"-prefixed
        // method, which is wrong here since this can insert a row.
        Task<ConversationDto> OpenAsync(Guid reportId);

        // The current user's own conversations, each with its latest
        // message (if any) for a list-page preview.
        Task<List<ConversationDto>> GetListAsync();

        // Full message history for one conversation.
        Task<ConversationDto> GetAsync(Guid id);

        Task<ConversationMessageDto> SendMessageAsync(Guid id, SendMessageInputDto input);

        // Phase 2 (voice calling), 1:1 only. Every method verifies the
        // current user is a participant (same as messaging) before
        // returning Agora credentials - never the App Certificate, only
        // an App ID + short-lived per-user RTC token. Starting a call that
        // is already ringing/connected for this conversation is
        // idempotent (repeated clicks reuse the same call), never starts
        // a second concurrent one.
        Task<CallCredentialsDto> StartCallAsync(Guid id);

        // The callee's Accept action - joins the same call the caller
        // started, transitioning it to Connected.
        Task<CallCredentialsDto> JoinCallAsync(Guid id);

        // Covers both an explicit hangup and a decline - the frontend
        // decides which copy to show; server-side it's the same action.
        Task EndCallAsync(Guid id);
    }
}
