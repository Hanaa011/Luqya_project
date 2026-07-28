import { api } from "./httpClient";

// ForgeService.CategoryGET2Async(...) -> GET api/app/category -> PagedResultDto<CategoryDto>
export function listCategories({ sorting, skipCount, maxResultCount } = {}, signal) {
  return api.get("/api/app/category", { sorting, skipCount, maxResultCount }, signal);
}

// ForgeService.CategoryPOSTAsync(CreateUpdateCategoryDto) -> POST api/app/category
export function createCategory({ name, icon }) {
  return api.post("/api/app/category", { name, icon });
}
