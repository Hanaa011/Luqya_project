import { api } from "./httpClient";

// ForgeService.LocationGET2Async(...) -> GET api/app/location -> PagedResultDto<LocationDto>
export function listLocations({ sorting, skipCount, maxResultCount } = {}, signal) {
  return api.get("/api/app/location", { sorting, skipCount, maxResultCount }, signal);
}

// ForgeService.LocationPOSTAsync(CreateUpdateLocationDto) -> POST api/app/location
export function createLocation({ placeName }) {
  return api.post("/api/app/location", { placeName });
}
