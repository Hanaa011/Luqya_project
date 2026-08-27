import { api } from "./httpClient";

// ForgeService.SearchAllAsync(AiSearchInputDto) -> POST api/app/ai-search/search
// -> AiSearchResponseDto { reply, shouldMatch, extractedType/Description/
// Color/Location, followUpPrompt, results: AiSearchResultDto[] }
// Verified against LostFound.Application.Contracts/AI/Dtos/AiSearchInputDto.cs
// and AiSearchResponseDto.cs: Type is a nullable ReportType? on the backend —
// omit it entirely to search both Lost and Found reports, only send it when
// the caller explicitly wants one type. `context` is the previous turn's
// extracted fields ({type, description, color, location} or null/undefined
// on a fresh conversation) — a single concise current value each, echoed
// straight from the prior response, never accumulated client-side.
export function aiSearch({ text, imageBase64, type, maxResults = 10, minimumScorePercentage, context }) {
  return api.post("/api/app/ai-search/search", {
    text,
    imageBase64,
    type,
    maxResults,
    minimumScorePercentage,
    contextType: context?.type,
    contextDescription: context?.description,
    contextColor: context?.color,
    contextLocation: context?.location,
    // "lost"/"found" - a direction already confirmed by an earlier message
    // in this conversation (see AiSearchInputDto.ContextReportKind). Keeps
    // a self-corrected direction from being lost on a later turn that only
    // adds a location/color and doesn't restate the user's role.
    contextReportKind: context?.reportKind,
    // The item's name in the language the searcher wrote in (see
    // AiSearchInputDto.ContextItemNameLocal) - a real search-quality signal
    // (candidate descriptions are often bilingual), not just wording, so
    // this must survive follow-up turns exactly like the other fields.
    contextItemNameLocal: context?.itemNameLocal,
  });
}

// AiSearchAppService.SearchAsync does `Convert.FromBase64String(input.ImageBase64)`
// server-side — base64 is a verified, real field for this endpoint.
// (CreateReportDto.imagePath takes a blob name instead, from
// reports.js's uploadReportImage() — see Task B.)
export function imageFileToBase64(file) {
  return new Promise((resolve, reject) => {
    if (!file) return resolve(undefined);
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).split(",")[1]);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}
