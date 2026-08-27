import { api } from "./httpClient";

// IConversationAppService.OpenAsync(reportId) -> POST
// api/app/conversation/open/{reportId} -> ConversationDto (verified via
// swagger — ABP binds the single Guid parameter into the URL path here,
// not the request body). The "This is my item" entry point — creates or
// reuses the one private conversation for (reportId, current user, report
// owner). Never exposes phone/email; see ConversationDto.otherParticipantName.
export function openConversation(reportId) {
  return api.post(`/api/app/conversation/open/${reportId}`);
}

// IConversationAppService.GetListAsync() -> GET api/app/conversation ->
// ConversationDto[]. The current user's own conversations, each with its
// latest message (if any) for a list-page preview.
export function listConversations(signal) {
  return api.get("/api/app/conversation", undefined, signal);
}

// IConversationAppService.GetAsync(id) -> GET api/app/conversation/{id}
// -> ConversationDto with full message history.
export function getConversation(id, signal) {
  return api.get(`/api/app/conversation/${id}`, undefined, signal);
}

// IConversationAppService.SendMessageAsync(id, SendMessageInputDto) ->
// POST api/app/conversation/{id}/send-message -> ConversationMessageDto.
export function sendConversationMessage(id, text) {
  return api.post(`/api/app/conversation/${id}/send-message`, { text });
}
