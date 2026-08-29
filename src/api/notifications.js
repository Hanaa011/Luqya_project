import { api } from "./httpClient";

// ForgeService.NotificationGETAsync(...) -> GET api/app/notification -> PagedResultDto<NotificationDto>
export function listNotifications({ reporterId, sorting, skipCount, maxResultCount } = {}, signal) {
  return api.get("/api/app/notification", { reporterId, sorting, skipCount, maxResultCount }, signal);
}

// ForgeService.NotificationMyListAsync(...) -> GET api/app/notification/my-list -> PagedResultDto<NotificationDto>
// Identity-keyed notifications (currently: missed calls) for the current
// logged-in user - scoped server-side to CurrentUser.Id, no reporterId needed.
export function listMyNotifications({ sorting, skipCount, maxResultCount } = {}, signal) {
  return api.get("/api/app/notification/my-list", { sorting, skipCount, maxResultCount }, signal);
}

// ForgeService.NotificationPOSTAsync(CreateNotificationDto) -> POST api/app/notification
export function createNotification({ reporterId, reportId, title, message }) {
  return api.post("/api/app/notification", { reporterId, reportId, title, message });
}

// ForgeService.MarkAsReadAsync(id) -> POST api/app/notification/{id}/mark-as-read
export function markNotificationAsRead(id) {
  return api.post(`/api/app/notification/${id}/mark-as-read`);
}
