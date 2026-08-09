import { useEffect, useState, useCallback } from "react";
import * as authApi from "../api/auth";
import { onUnauthorized, ApiError } from "../api/httpClient";
import { getToken, clearToken } from "../api/tokenStore";
import { clearKnownReporterId } from "../api/reporterIdentity";
import { getUserIdFromToken } from "./jwt";
import { AuthContext } from "./authContext";

export function AuthProvider({ children }) {
  const [profile, setProfile] = useState(null);
  const [userId, setUserId] = useState(null);
  const [loading, setLoading] = useState(true);

  const refreshProfile = useCallback(async () => {
    try {
      const data = await authApi.getMyProfile();
      setProfile(data);
      setUserId(getUserIdFromToken(getToken()));
      return data;
    } catch (err) {
      if (err instanceof ApiError && err.isUnauthorized) {
        // The token itself is invalid/expired at the server — not just a
        // UI-state thing. If we only clear React state here and leave the
        // token sitting in storage, httpClient keeps attaching it to
        // every subsequent request (including a "guest" report
        // submission the UI now shows), and the backend authenticates
        // that request as the old account. Clear the actual token too.
        clearToken();
        clearKnownReporterId();
        setProfile(null);
        setUserId(null);
        return null;
      }
      throw err;
    }
  }, []);

  useEffect(() => {
    // Bearer-token session: only ask the backend for a profile if a token
    // was actually persisted from a previous login — a guest who never
    // logged in shouldn't fire an API call (and a guaranteed 401) on
    // every page load.
    let cancelled = false;

    Promise.resolve().then(() => {
      if (cancelled) return;

      if (getToken()) {
        refreshProfile().finally(() => !cancelled && setLoading(false));
      } else {
        setLoading(false);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [refreshProfile]);

  useEffect(() => {
    return onUnauthorized(() => {
      // Same reasoning as above: a 401 from ANY endpoint means the
      // server no longer considers this token valid, so it must not be
      // sent again — otherwise the app can show "logged out" while
      // still authenticating requests as the previous account.
      clearToken();
      clearKnownReporterId();
      setProfile(null);
      setUserId(null);
    });
  }, []);

  async function login(credentials) {
    await authApi.login(credentials);
    return refreshProfile();
  }

  async function register(payload) {
    return authApi.register(payload);
  }

  function logout() {
    // No real async work here — authApi.logout() only clears local
    // storage/token synchronously. Previously this was `async function
    // logout() { await authApi.logout(); ... }`: awaiting ANY promise,
    // even one already resolved, unconditionally defers everything after
    // it to a microtask. That gap — navigate("/") running synchronously
    // in the click handler, profile clearing one microtask later — was
    // the actual root cause of the login-page flash on logout: a real,
    // if narrow, window where the route hadn't fully settled while auth
    // state was still mid-transition. Doing this synchronously closes
    // that window entirely rather than trying to out-time it.
    authApi.logout();
    setProfile(null);
    setUserId(null);
  }

  return (
    <AuthContext.Provider
      value={{
        profile,
        userId,
        loading,
        login,
        register,
        logout,
        refreshProfile,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
