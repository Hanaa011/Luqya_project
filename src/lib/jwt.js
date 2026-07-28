/**
 * Reads claims out of a JWT's payload segment — no signature
 * verification, because none is needed: this is only used to read the
 * authenticated user's own id (`sub`) for client-side display/ownership
 * comparison, never for an authorization decision. Every real
 * authorization check still happens server-side on every request
 * regardless of what this returns.
 *
 * Returns null (never throws) if the token isn't a decodable JWT — some
 * OAuth servers issue opaque reference tokens instead, and this must
 * fail closed, not crash the app, if that's the case here.
 */
export function decodeJwtClaims(token) {
  if (!token || typeof token !== "string") return null;

  const parts = token.split(".");
  if (parts.length !== 3) return null;

  try {
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = payload.padEnd(payload.length + ((4 - (payload.length % 4)) % 4), "=");
    const json = decodeURIComponent(
      atob(padded)
        .split("")
        .map((c) => "%" + c.charCodeAt(0).toString(16).padStart(2, "0"))
        .join("")
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

/** The standard OIDC "subject" claim — matches ASP.NET Core Identity's
 * user Id, which is exactly what populates ReportDto.creatorId server-side
 * (ABP's ICurrentUser.Id) for any report created while authenticated. */
export function getUserIdFromToken(token) {
  const claims = decodeJwtClaims(token);
  return claims?.sub ?? null;
}
