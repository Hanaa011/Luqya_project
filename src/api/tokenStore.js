const STORAGE_KEY = "luqya-access-token";

/**
 * The one place the access token is read from, written to, and cleared.
 * Nothing else in the app touches localStorage directly for auth state —
 * httpClient reads getToken() on every request, auth.jsx calls setToken()
 * on login and clearToken() on logout/401.
 */
let inMemoryToken = null;

export function getToken() {
  if (inMemoryToken) return inMemoryToken;
  inMemoryToken = window.localStorage.getItem(STORAGE_KEY);
  return inMemoryToken;
}

export function setToken(token) {
  inMemoryToken = token;
  if (token) {
    window.localStorage.setItem(STORAGE_KEY, token);
  } else {
    window.localStorage.removeItem(STORAGE_KEY);
  }
}

export function clearToken() {
  setToken(null);
}
