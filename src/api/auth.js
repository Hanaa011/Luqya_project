import { api } from "./httpClient";
import { API_BASE_URL, APP_NAME, OAUTH_CLIENT_ID, OAUTH_SCOPE } from "./config";
import { setToken, clearToken } from "./tokenStore";
import { clearKnownReporterId } from "./reporterIdentity";

/**
 * Every function here maps 1:1 to a real, verified backend endpoint — see
 * the comment above each one. login()/logout() were previously built
 * against ForgeService's cookie-based /api/account/login, which is wrong
 * for this backend: the API host only validates OpenIddict Bearer tokens
 * (confirmed via ForwardIdentityAuthenticationForBearer +
 * UseAbpOpenIddictValidation in ForgeHttpApiHostModule.cs), so
 * authentication goes through the real OAuth2 token endpoint instead.
 */

// ForgeService.RegisterAsync(RegisterDto) -> POST api/account/register
export function register({ userName, emailAddress, password }) {
  return api.post("/api/account/register", {
    userName,
    emailAddress,
    password,
    appName: APP_NAME,
  });
}

/**
 * POST /connect/token (OpenIddict, verified via OpenIddictDataSeedContributor.cs)
 * grant_type=password, client_id=Forge_App, scope=Forge — no client
 * secret (public client). Not requesting offline_access: "Forge" is the
 * only scope confirmed seeded for this client, and requesting an
 * unverified scope risks the whole token request being rejected.
 */
export async function login({ userNameOrEmailAddress, password }) {
  const body = new URLSearchParams({
    grant_type: "password",
    username: userNameOrEmailAddress,
    password,
    client_id: OAUTH_CLIENT_ID,
    scope: OAUTH_SCOPE,
  });

  const response = await fetch(`${API_BASE_URL}/connect/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: body.toString(),
  });

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    const error = new Error(data?.error_description || "Login failed");
    error.reason = data?.error === "invalid_grant" ? "invalidCredentials" : data?.error || "unknown";
    throw error;
  }

  setToken(data.access_token);
  return data;
}

// No server-side logout/revoke endpoint is verified for this OAuth client
// (nothing in ForgeService or the backend source exposes /connect/logout
// or a token-revocation action) — logging out is a client-side action:
// discard the token so it's no longer sent on any request.
export function logout() {
  clearToken();
  clearKnownReporterId();
  return Promise.resolve();
}

// ForgeService.SendPasswordResetCodeAsync(SendPasswordResetCodeDto)
export function sendPasswordResetCode({ email, returnUrl, returnUrlHash }) {
  return api.post("/api/account/send-password-reset-code", {
    email,
    appName: APP_NAME,
    returnUrl,
    returnUrlHash,
  });
}

// ForgeService.VerifyPasswordResetTokenAsync(VerifyPasswordResetTokenInput) -> bool
export function verifyPasswordResetToken({ userId, resetToken }) {
  return api.post("/api/account/verify-password-reset-token", { userId, resetToken });
}

// ForgeService.ResetPasswordAsync(ResetPasswordDto)
export function resetPassword({ userId, resetToken, password }) {
  return api.post("/api/account/reset-password", { userId, resetToken, password });
}

// ForgeService.MyProfileGETAsync() -> GET api/account/my-profile -> ProfileDto
export function getMyProfile(signal) {
  return api.get("/api/account/my-profile", undefined, signal);
}

// ForgeService.MyProfilePUTAsync(UpdateProfileDto)
export function updateMyProfile(profile) {
  return api.put("/api/account/my-profile", profile);
}

// ForgeService.ChangePasswordAsync(ChangePasswordInput)
export function changePassword({ currentPassword, newPassword }) {
  return api.post("/api/account/my-profile/change-password", { currentPassword, newPassword });
}

/**
 * NOTE — integration gap: VerifyPasswordResetTokenInput/ResetPasswordDto
 * both require a userId, but SendPasswordResetCodeAsync returns nothing,
 * so the frontend has no official way to learn the userId for an
 * anonymous "forgot password" visitor. This falls back to
 * ForgeService.ByEmailAsync (api/identity/users/by-email/{email}), which
 * on most ABP setups requires an authenticated admin permission
 * (AbpIdentity.Users) — it will likely 401/403 for a logged-out visitor.
 * Flagging for the backend team: either expose an anonymous endpoint that
 * resolves the code directly, or have the reset flow accept email instead
 * of userId.
 */
export function getUserByEmailBestEffort(email) {
  return api.get(`/api/identity/users/by-email/${encodeURIComponent(email)}`);
}
