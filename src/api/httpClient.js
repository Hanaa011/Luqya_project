import { API_BASE_URL } from "./config";
import { getToken, clearToken } from "./tokenStore";

/**
 * Normalized error shape for every API call in the app. ABP returns
 * errors as { error: { code, message, details, validationErrors } } —
 * ApiError flattens that so a page never has to know the envelope shape.
 */
export class ApiError extends Error {
  constructor({ status, code, message, details, validationErrors }) {
    super(message || "Request failed");
    this.name = "ApiError";
    this.status = status;
    this.code = code ?? null;
    this.details = details ?? null;
    this.validationErrors = validationErrors ?? [];
  }

  get isUnauthorized() {
    return this.status === 401;
  }

  get isForbidden() {
    return this.status === 403;
  }

  get isValidation() {
    return this.status === 400 && this.validationErrors.length > 0;
  }

  get isServerError() {
    return this.status >= 500;
  }
}

let acceptLanguage = "ar";

// Called from the i18n provider whenever the active locale changes, so
// every subsequent request tells ABP which culture to localize with —
// independent of any one page's own translation dictionary.
export function setAcceptLanguage(localeCode) {
  acceptLanguage = localeCode;
}

// Fired on any 401 so a single place (the auth provider) can react —
// clear local user state and redirect to /auth/login — instead of every
// page needing its own "am I still logged in" check.
const unauthorizedListeners = new Set();
export function onUnauthorized(listener) {
  unauthorizedListeners.add(listener);
  return () => unauthorizedListeners.delete(listener);
}

async function parseErrorBody(response) {
  try {
    const data = await response.json();
    const err = data?.error;
    return {
      code: err?.code,
      message: err?.message || err?.details,
      details: err?.details,
      validationErrors: err?.validationErrors ?? [],
    };
  } catch {
    return { message: response.statusText };
  }
}

function buildQuery(params) {
  if (!params) return "";
  const search = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") return;
    search.set(key, value);
  });

  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

/**
 * request() is the one function every src/api/*.js module calls through.
 * It mirrors what ForgeService's generated methods do under the hood
 * (JSON body, Accept: application/json, ABP's error envelope) but as a
 * small reusable fetch wrapper instead of a generated C# class.
 */
export async function request(path, { method = "GET", params, body, signal } = {}) {
  const url = `${API_BASE_URL}${path}${buildQuery(params)}`;
  const token = getToken();

  let response;
  try {
    response = await fetch(url, {
      method,
      signal,
      headers: {
        Accept: "application/json",
        "Accept-Language": acceptLanguage,
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (networkError) {
    throw new ApiError({ status: 0, message: networkError.message, code: "NETWORK_ERROR" });
  }

  if (response.status === 401) {
    // A 401 on a request that carried a token means that token is dead —
    // clear it so the app doesn't keep sending a known-invalid header.
    if (token) clearToken();
    unauthorizedListeners.forEach((listener) => listener());
  }

  if (!response.ok) {
    const errInfo = await parseErrorBody(response);
    throw new ApiError({ status: response.status, ...errInfo });
  }

  if (response.status === 204) return null;

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/json")) return null;

  return response.json();
}

export const api = {
  get: (path, params, signal) => request(path, { method: "GET", params, signal }),
  post: (path, body, signal) => request(path, { method: "POST", body: body ?? {}, signal }),
  put: (path, body, signal) => request(path, { method: "PUT", body: body ?? {}, signal }),
  del: (path, signal) => request(path, { method: "DELETE", signal }),
};
