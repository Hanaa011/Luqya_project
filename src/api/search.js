import { api } from "./httpClient";

// ForgeService.SearchAllAsync(AiSearchInputDto) -> POST api/app/ai-search/search
// -> ICollection<AiSearchResultDto>
// Verified against LostFound.Application.Contracts/AI/Dtos/AiSearchInputDto.cs:
// Type is a nullable ReportType? on the backend — omit it entirely to
// search both Lost and Found reports, only send it when the caller
// explicitly wants one type.
export function aiSearch({ text, imageBase64, type, maxResults = 10, minimumScorePercentage }) {
  return api.post("/api/app/ai-search/search", {
    text,
    imageBase64,
    type,
    maxResults,
    minimumScorePercentage,
  });
}

// AiSearchAppService.SearchAsync does `Convert.FromBase64String(input.ImageBase64)`
// server-side — base64 is a verified, real field for this endpoint
// specifically (unlike CreateReportDto.imagePath, which is not).
export function imageFileToBase64(file) {
  return new Promise((resolve, reject) => {
    if (!file) return resolve(undefined);
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).split(",")[1]);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}
