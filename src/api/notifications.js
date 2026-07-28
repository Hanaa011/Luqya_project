import { api } from "./httpClient";

// ForgeService.NotificationGETAsync(...) -> GET api/app/notification -> PagedResultDto<NotificationDto>
export function listNotifications({ reporterId, sorting, skipCount, maxResultCount } = {}, signal) {
  return api.get("/api/app/notification", { reporterId, sorting, skipCount, maxResultCount }, signal);
}

// ForgeService.NotificationPOSTAsync(CreateNotificationDto) -> POST api/app/notification
export function createNotification({ reporterId, reportId, title, message }) {
  return api.post("/api/app/notification", { reporterId, reportId, title, message });
}

// ForgeService.MarkAsReadAsync(id) -> POST api/app/notification/{id}/mark-as-read
export function markNotificationAsRead(id) {
  return api.post(`/api/app/notification/${id}/mark-as-read`);
}
