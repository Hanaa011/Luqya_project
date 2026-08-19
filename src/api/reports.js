import { api } from "./httpClient";
import { API_BASE_URL } from "./config";

// ReportAppService.GetListAsync(GetReportListDto) -> GET api/app/report -> PagedResultDto<ReportDto>
// NOTE: GetReportListDto has no CreatorId property, and GetListAsync's
// .WhereIf(...) chain never filters by creator — there is no server-side
// "my reports" filter via CreatorId. Ownership filtering fetches a page
// and filters client-side by report.creatorId instead — see
// lib/myReports.js. `reporterId` here is a real, separate, working
// server-side filter, kept for contact/reporter-record purposes only,
// not account ownership.
export function listReports({ filter, type, status, locationId, reporterId, categoryId, sorting, skipCount, maxResultCount } = {}, signal) {
  return api.get(
    "/api/app/report",
    { filter, type, status, locationId, reporterId, categoryId, sorting, skipCount, maxResultCount },
    signal
  );
}

// ForgeService.ReportGETAsync(id) -> GET api/app/report/{id} -> ReportDto
export function getReport(id, signal) {
  return api.get(`/api/app/report/${id}`, undefined, signal);
}

// IReportAppService.UploadImageAsync(byte[] imageBytes) -> POST api/app/report/upload-image
// -> string (the blob name — pass it as CreateReportDto.imagePath /
// UpdateReportDto.imagePath). The body is the base64 image string itself,
// NOT wrapped in an object — ASP.NET Core binds a single byte[] parameter
// from a raw JSON string body. Verified live against the running host
// (Task B, see SemanticReports/Image-Search-Implementation-Report.md).
export function uploadReportImage(imageBase64) {
  return api.post("/api/app/report/upload-image", imageBase64);
}

// Task 6 (Phase 3 Part 2 real-world validation): ReportDto.imagePath is a bare
// blob name (e.g. "af92694fc89..."), not a URL — used directly as an <img src>
// it can only ever 404. GET api/app/report/image/{blobName} is a real MVC
// controller (ReportImagesController, LostFound.HttpApi), not an
// ApplicationService, specifically so it can return actual image bytes with a
// real Content-Type instead of a JSON/base64 wrapper. Anonymous, so a plain
// <img> tag (which can't attach the Bearer token) can load it directly.
export function reportImageUrl(imagePath) {
  if (!imagePath) return null;
  return `${API_BASE_URL}/api/app/report/image/${encodeURIComponent(imagePath)}`;
}

// ForgeService.ReportPOSTAsync(CreateReportDto) -> POST api/app/report -> ReportDto
// Verified against LostFound.Application.Contracts/Reports/Dtos/CreateReportDto.cs —
// reporterName/reporterPhone/reporterEmail/preferredContact are real,
// current fields (ignored server-side for authenticated users, required
// — phone specifically — for guests). `imagePath` IS a real, verified field
// (see Task B) — set it from uploadReportImage()'s returned blob name to
// actually persist an attached image with the report.
export function createReport({
  locationId,
  locationDetails,
  type,
  description,
  lostFoundDate,
  imagePath,
  isItemWithFinder,
  pickupLocation,
  reporterName,
  reporterPhone,
  reporterEmail,
  preferredContact,
}) {
  return api.post("/api/app/report", {
    locationId,
    locationDetails,
    type,
    description,
    lostFoundDate,
    imagePath,
    isItemWithFinder,
    pickupLocation,
    reporterName,
    reporterPhone,
    reporterEmail,
    preferredContact,
  });
}

// ForgeService.ReportPUTAsync(id, UpdateReportDto) -> PUT api/app/report/{id} -> ReportDto
export function updateReport(id, patch) {
  return api.put(`/api/app/report/${id}`, patch);
}

// ForgeService.ReportDELETEAsync(id) -> DELETE api/app/report/{id}
export function deleteReport(id) {
  return api.del(`/api/app/report/${id}`);
}

// ForgeService.ReportAnalyticsAsync(topN?) -> GET api/app/report-analytics -> ReportAnalyticsDto
export function getReportAnalytics(topN, signal) {
  return api.get("/api/app/report-analytics", { topN }, signal);
}