/**
 * Single source of truth for where the ABP backend lives. Set
 * VITE_API_BASE_URL in a .env file (e.g. VITE_API_BASE_URL=https://localhost:44300)
 * — everything under src/api/ reads from here, nothing hardcodes a host.
 */
/**
 * Single source of truth for where the ABP backend lives. Set
 * VITE_API_BASE_URL in a .env file — everything under src/api/ imports
 * this constant, nothing hardcodes a host anywhere else. Trailing slash
 * is stripped defensively so `${API_BASE_URL}/api/app/report` never
 * produces a doubled `//`.
 */
const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
export const API_BASE_URL = rawBaseUrl.replace(/\/+$/, "");

// RegisterDto/SendPasswordResetCodeDto both require an "appName". ABP's
// Account module resolves this against a registered OpenIddict
// application (used to build confirmation/redirect links), and the only
// two applications seeded on this backend are "Forge_App" and
// "Forge_Swagger" (confirmed in OpenIddictDataSeedContributor.cs) — so
// "Forge_App" is the evidence-backed value, not a guess of "Luqya".
// Override via VITE_ABP_APP_NAME if the backend team confirms otherwise.
export const APP_NAME = import.meta.env.VITE_ABP_APP_NAME ?? "Forge_App";

// The OpenIddict public client seeded for the SPA (password + refresh
// token grants, no secret). Confirmed in OpenIddictDataSeedContributor.cs.
export const OAUTH_CLIENT_ID = import.meta.env.VITE_OAUTH_CLIENT_ID ?? "Forge_App";
export const OAUTH_SCOPE = import.meta.env.VITE_OAUTH_SCOPE ?? "Forge";
