import { api } from "./httpClient";

// IConversationAppService.StartCallAsync(id) -> POST
// api/app/conversation/{id}/start-call -> CallCredentialsDto. The caller's
// entry point (the existing phone button) - never returns the Agora App
// Certificate, only an App ID + short-lived per-user RTC token.
export function startCall(conversationId) {
  return api.post(`/api/app/conversation/${conversationId}/start-call`);
}

// IConversationAppService.JoinCallAsync(id) -> POST
// api/app/conversation/{id}/join-call -> CallCredentialsDto. The callee's
// Accept action.
export function joinCall(conversationId) {
  return api.post(`/api/app/conversation/${conversationId}/join-call`);
}

// IConversationAppService.EndCallAsync(id) -> POST
// api/app/conversation/{id}/end-call. Covers both hangup and decline.
export function endCall(conversationId) {
  return api.post(`/api/app/conversation/${conversationId}/end-call`);
}
